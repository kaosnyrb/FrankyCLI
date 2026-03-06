using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;

namespace Retrograde.Nouns
{
    /// <summary>
    /// Marker interface for all Noun classes. Exposes the primary generated record's
    /// EditorID and FormKey without requiring callers to know the concrete record type.
    /// Useful for logging, auditing, and generic registries.
    /// </summary>
    public interface INoun
    {
        string? EditorID { get; }
        FormKey FormKey { get; }
    }

    /// <summary>
    /// Typed extension of INoun that exposes the primary generated record.
    /// Lets callers hold a reference to any Noun and access its result uniformly.
    /// </summary>
    public interface INoun<T> : INoun where T : class, IMajorRecordGetter
    {
        T Result { get; }
    }
}
