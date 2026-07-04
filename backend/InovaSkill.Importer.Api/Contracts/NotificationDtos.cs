using System;
using System.Collections.Generic;

namespace InovaSkill.Importer.Api.Contracts;

public sealed record NotificationDto(
    long Id,
    long UserId,
    string Title,
    string Message,
    string Type,
    string Priority,
    string Status,
    string RelatedLink,
    string RelatedEntity,
    long? RelatedEntityId,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record NotificationListDto(
    int Total,
    int UnreadCount,
    IReadOnlyList<NotificationDto> Notifications);

public sealed record UnreadCountDto(
    int Count);
