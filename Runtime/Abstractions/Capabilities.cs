using System;
using UnityEngine;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>Text content capability (labels, buttons with text, etc.).</summary>
    public interface IUITextCapability
    {
        string Text { get; set; }
    }

    /// <summary>Scalar value capability (progress bars, sliders, radial fills).</summary>
    public interface IUIValueCapability
    {
        float Value { get; set; }
        float Min { get; set; }
        float Max { get; set; }
    }

    /// <summary>Show / hide capability, independent of transform opacity.</summary>
    public interface IUIVisibilityCapability
    {
        bool Visible { get; set; }
    }

    /// <summary>Enable / disable interaction capability.</summary>
    public interface IUIInteractableCapability
    {
        bool Interactable { get; set; }
    }

    /// <summary>Click event capability.</summary>
    public interface IUIClickCapability
    {
        event Action Clicked;
    }

    /// <summary>Styling capability (css class toggling + token application).</summary>
    public interface IUIStyleCapability
    {
        void SetClass(string className, bool on);
        void ApplyToken(string tokenKey, string value);
    }

    /// <summary>
    /// Transform capability used by the motion system. All motion is expressed
    /// through this capability so the Motion module never touches a backend type.
    /// </summary>
    public interface IUITransformCapability
    {
        float Opacity { get; set; }
        Vector2 Position { get; set; }
        Vector3 Scale { get; set; }
        float Rotation { get; set; }
    }

    /// <summary>Element size capability (RectTransform.sizeDelta / UI Toolkit width+height).</summary>
    public interface IUISizeCapability
    {
        Vector2 SizeDelta { get; set; }
    }

    /// <summary>Portable color channels used by typed style overrides.</summary>
    public interface IUIColorCapability
    {
        Color BackgroundColor { get; set; }
        Color TextColor { get; set; }
    }

    /// <summary>Portable typography subset shared by uGUI and UI Toolkit.</summary>
    public interface IUITypographyCapability
    {
        float FontSize { get; set; }
    }
}
