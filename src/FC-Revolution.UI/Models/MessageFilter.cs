using System;

namespace FC_Revolution.UI.Models;

public sealed class MessageFilter
{
    public MessageCategory? Category { get; init; }

    public MessageSeverity? MinSeverity { get; init; }

    public DateTime? Since { get; init; }

    public bool? OnlyUnread { get; init; }

    public bool Matches(TaskMessage message)
    {
        if (Category.HasValue && message.Category != Category.Value)
            return false;

        if (MinSeverity.HasValue && GetSeverityRank(message.Severity) < GetSeverityRank(MinSeverity.Value))
            return false;

        if (Since.HasValue && message.Timestamp < Since.Value)
            return false;

        if (OnlyUnread == true && message.IsRead)
            return false;

        if (OnlyUnread == false && !message.IsRead)
            return false;

        return true;
    }

    private static int GetSeverityRank(MessageSeverity severity) => severity switch
    {
        MessageSeverity.Success => 1,
        MessageSeverity.Warning => 2,
        MessageSeverity.Error => 3,
        _ => 0
    };
}
