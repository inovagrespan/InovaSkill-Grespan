using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Api.Services;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InovaSkill.Importer.Tests.Api;

public sealed class MeetingsControllerTests
{
    [Fact]
    public async Task GetMeetings_ReturnsEmptyList_WhenNoMeetingsExist()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, AppUserRoles.Diretor);

        var result = await controller.GetMeetings();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var meetings = Assert.IsType<List<MeetingListDto>>(ok.Value);
        Assert.Empty(meetings);
    }

    [Fact]
    public async Task CreateMeeting_ReturnsForbidden_WhenUserIsNotDirector()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, AppUserRoles.Gestor);
        var request = new CreateMeetingRequestDto(
            "Test", "Desc", "Reason", [], null, "", "", []);

        var result = await controller.CreateMeeting(request);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task CreateMeeting_CreatesMeetingAndNotifiesParticipants()
    {
        await using var db = CreateDb();
        var director = new AppUser
        {
            Name = "Diretor",
            Email = "diretor@test.com",
            Role = AppUserRoles.Diretor
        };
        db.AppUsers.Add(director);
        var participant = new AppUser
        {
            Name = "Gestor",
            Email = "gestor@test.com",
            Role = AppUserRoles.Gestor
        };
        db.AppUsers.Add(participant);
        await db.SaveChangesAsync();

        var controller = CreateController(db, AppUserRoles.Diretor, director.Id, director.Name);
        var request = new CreateMeetingRequestDto(
            "Reunião de teste",
            "Descrição da reunião",
            "Motivo importante",
            [participant.Id],
            DateTime.UtcNow.AddDays(1),
            "Contexto inicial",
            "Produção, Logística",
            []);

        var result = await controller.CreateMeeting(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var meeting = Assert.IsType<MeetingDetailDto>(ok.Value);
        Assert.Equal("Reunião de teste", meeting.Title);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Equal(MeetingStage.Context, meeting.CurrentStage);
        Assert.Single(meeting.Participants);
        Assert.Contains(await db.MeetingHistories.ToListAsync(), h => h.EventType == MeetingHistoryEvent.Created);

        var notifications = await db.Notifications.CountAsync();
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task UpdateStage_AdvancesToNextStage()
    {
        await using var db = CreateDb();
        var meeting = new Meeting
        {
            Title = "Test",
            Description = "Desc",
            Reason = "Reason",
            Status = MeetingStatus.InProgress,
            CurrentStage = MeetingStage.Context,
            CreatedByUserId = 1,
            CreatedByName = "Diretor",
            Participants = []
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        meeting.Participants.Add(new MeetingParticipant { UserId = 2, UserName = "Gestor", UserEmail = "g@test.com", UserRole = AppUserRoles.Gestor, UserSector = "Produção" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, AppUserRoles.Diretor);
        var request = new UpdateMeetingStageRequestDto(MeetingStage.Discussion);

        var result = await controller.UpdateStage(meeting.Id, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<MeetingDetailDto>(ok.Value);
        Assert.Equal(MeetingStage.Discussion, updated.CurrentStage);
        Assert.Contains(await db.MeetingHistories.ToListAsync(), h => h.EventType == MeetingHistoryEvent.StageChanged);
    }

    [Fact]
    public async Task GetMeeting_ReturnsForbidden_WhenUserWasNotInvited()
    {
        await using var db = CreateDb();
        var meeting = new Meeting
        {
            Title = "Test",
            Description = "Desc",
            Reason = "Reason",
            Status = MeetingStatus.InProgress,
            CurrentStage = MeetingStage.Context,
            CreatedByUserId = 1,
            CreatedByName = "Diretor",
            Participants = [new MeetingParticipant { UserId = 2, UserName = "Gestor", UserEmail = "g@test.com", UserRole = AppUserRoles.Gestor }]
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var controller = CreateController(db, AppUserRoles.Gestor, userId: 99, userName: "Intruso");

        var result = await controller.GetMeeting(meeting.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GenerateAiAnalysis_PersistsOneAnalysisPerApprovedProblem()
    {
        await using var db = CreateDb();
        var meeting = new Meeting
        {
            Title = "Test",
            Description = "Desc",
            Reason = "Reason",
            Status = MeetingStatus.InProgress,
            CurrentStage = MeetingStage.AiAnalysis,
            CreatedByUserId = 1,
            CreatedByName = "Diretor",
            Participants = [],
            Problems =
            [
                new MeetingProblem
                {
                    Sector = "Produção",
                    Description = "Produção baixa",
                    Severity = ProblemSeverity.High,
                    Origin = ProblemOrigin.Discussion,
                    CreatedByUserId = 1,
                    CreatedByName = "Diretor",
                    ApprovedByDirector = true
                }
            ]
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var controller = CreateController(db, AppUserRoles.Diretor);

        var result = await controller.GenerateAiAnalysis(meeting.Id, new GenerateMeetingAiAnalysisRequestDto(Force: true, Justification: "MVP"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var analyses = Assert.IsAssignableFrom<IReadOnlyList<MeetingAiAnalysisDto>>(ok.Value);
        Assert.Single(analyses);
        Assert.Contains(await db.MeetingHistories.ToListAsync(), h => h.EventType == MeetingHistoryEvent.AiAnalysisGenerated);
    }

    [Fact]
    public async Task AddComment_CreatesComment()
    {
        await using var db = CreateDb();
        var meeting = new Meeting
        {
            Title = "Test", Description = "Desc", Reason = "Reason",
            Status = MeetingStatus.InProgress, CurrentStage = MeetingStage.Discussion,
            CreatedByUserId = 1, CreatedByName = "Diretor", Participants = []
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var controller = CreateController(db, AppUserRoles.Diretor);
        var request = new AddCommentRequestDto("Comentário importante", MeetingStage.Discussion);

        var result = await controller.AddComment(meeting.Id, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var comment = Assert.IsType<MeetingCommentDto>(ok.Value);
        Assert.Equal("Comentário importante", comment.Message);
    }

    [Fact]
    public async Task ConcludeMeeting_MarksMeetingAsConcluded()
    {
        await using var db = CreateDb();
        var meeting = new Meeting
        {
            Title = "Test", Description = "Desc", Reason = "Reason",
            Status = MeetingStatus.InProgress, CurrentStage = MeetingStage.Actions,
            CreatedByUserId = 1, CreatedByName = "Diretor", Participants = []
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var controller = CreateController(db, AppUserRoles.Diretor);

        var result = await controller.ConcludeMeeting(meeting.Id);

        Assert.IsType<OkResult>(result);
        var saved = await db.Meetings.FirstAsync(m => m.Id == meeting.Id);
        Assert.Equal(MeetingStatus.Concluded, saved.Status);
        Assert.NotNull(saved.ConcludedAt);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"meetings-test-{Guid.NewGuid():N}")
            .Options;
        return new ImportDbContext(options);
    }

    private static MeetingsController CreateController(
        ImportDbContext db,
        string role,
        long userId = 1,
        string userName = "Diretor")
    {
        var controller = new MeetingsController(db, new MeetingAiAnalysisService(db));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("sub", userId.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim("name", userName),
                    new Claim(ClaimTypes.Name, userName),
                    new Claim("role", role),
                    new Claim(ClaimTypes.Role, role)
                ], "TestAuth"))
            }
        };
        return controller;
    }
}
