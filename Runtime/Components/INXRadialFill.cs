using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a radial / circular fill indicator (e.g. cooldowns).</summary>
    public interface INXRadialFill
    {
        IUIElementHandle Handle { get; }

        /// <summary>Fill amount in [0, 1].</summary>
        float Fill { get; set; }

        /// <summary>Clockwise when true.</summary>
        bool Clockwise { get; set; }
    }
}
