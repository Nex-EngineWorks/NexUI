#if !UNITY_2023_2_OR_NEWER
using System;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Stand-ins for the UXML source-generator attributes Unity 2023.2 introduced.
    /// </summary>
    /// <remarks>
    /// NexUI supports Unity 2022.3 LTS and Unity 6. Unity 6 declares custom elements with
    /// <c>[UxmlElement]</c> / <c>[UxmlAttribute]</c> and a source generator writes the plumbing;
    /// 2022.3 has neither the attributes nor the generator, so the same source would not compile.
    ///
    /// Two ways to bridge that. Wrapping all 37 attribute usages in <c>#if</c> would put a version
    /// check on every property in the file and make the elements themselves hard to read. Instead
    /// the attributes are declared here as no-ops for older editors, and the plumbing they would
    /// have generated is hand-written once per element in the matching <c>*.Legacy.cs</c> file.
    /// The element sources then stay written the modern way, with nothing version-specific in them.
    ///
    /// These types are deliberately <c>internal</c> and disappear entirely on 2023.2+, so no NexUI
    /// type is ever ambiguous with the real <c>UnityEngine.UIElements</c> attribute. Being in the
    /// same namespace as the elements is what makes them win name resolution over the
    /// <c>using UnityEngine.UIElements;</c> those files already have.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class UxmlElementAttribute : Attribute
    {
        public string name;

        public UxmlElementAttribute() { }

        public UxmlElementAttribute(string name) => this.name = name;
    }

    /// <inheritdoc cref="UxmlElementAttribute"/>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class UxmlAttributeAttribute : Attribute
    {
        public string name;

        public UxmlAttributeAttribute() { }

        public UxmlAttributeAttribute(string name) => this.name = name;
    }
}
#endif
