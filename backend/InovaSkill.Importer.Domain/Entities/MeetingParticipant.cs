namespace InovaSkill.Importer.Domain.Entities;

public sealed class MeetingParticipant
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string UserSector { get; set; } = string.Empty;
    public string RoleInMeeting { get; set; } = MeetingParticipantRole.Participant;
    public string ParticipationStatus { get; set; } = ParticipationStatusValue.Invited;
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }

    public Meeting Meeting { get; set; } = null!;
}

public static class MeetingParticipantRole
{
    public const string Director = "diretor";
    public const string Gestor = "gestor";
    public const string Participant = "participante";
}

public static class ParticipationStatusValue
{
    public const string Invited = "convidado";
    public const string Confirmed = "confirmado";
    public const string Declined = "recusado";
}
