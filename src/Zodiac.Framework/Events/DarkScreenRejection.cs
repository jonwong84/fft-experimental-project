using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum DarkScreenRejection
{
    None, InvalidManagedBinding, WorkerAlreadyAttached, ManagedTaskUpdateForbidden,
    EventRejected, TaskLedgerRejected,
}
