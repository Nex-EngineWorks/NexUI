using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Decouples theme propagation from screen management. The runtime registers providers that
    /// yield the surfaces it currently shows; the Theme module pulls from here when the active
    /// theme or an override changes. Neither side needs a reference to the other.
    /// </summary>
    public static class UIOpenSurfaceRegistry
    {
        private static readonly List<Func<IEnumerable<IUISurface>>> Providers =
            new List<Func<IEnumerable<IUISurface>>>();

        public static void RegisterProvider(Func<IEnumerable<IUISurface>> provider)
        {
            if (provider != null && !Providers.Contains(provider))
                Providers.Add(provider);
        }

        public static void UnregisterProvider(Func<IEnumerable<IUISurface>> provider)
        {
            if (provider != null) Providers.Remove(provider);
        }

        /// <summary>Every surface the registered providers currently show. Never null.</summary>
        public static IEnumerable<IUISurface> Collect()
        {
            // Snapshot so a provider may register/unregister during enumeration.
            for (var i = 0; i < Providers.Count; i++)
            {
                var provider = Providers[i];
                if (provider == null) continue;
                IEnumerable<IUISurface> surfaces;
                try { surfaces = provider(); }
                catch { continue; }
                if (surfaces == null) continue;
                foreach (var surface in surfaces)
                    if (surface != null) yield return surface;
            }
        }
    }
}
