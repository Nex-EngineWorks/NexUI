using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Templates
{
    /// <summary>
    /// A reusable authoring template that expands into a common game-UI structure
    /// (Pause Menu, Settings, Inventory Grid, Toast Queue, ...). The Designer uses a
    /// recipe to generate elements, ids, contracts, binding candidates and a focus
    /// graph in one step.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Template/UI Recipe", fileName = "NewUIRecipe")]
    public sealed class UIRecipe : ScriptableObject
    {
        public string recipeId;
        public string displayName;
        public List<UIRecipeElement> elements = new();
    }

    /// <summary>
    /// One element produced by a recipe. <see cref="elementType"/> is a backend-neutral
    /// type hint (e.g. "Panel", "Button", "Label"); the Designer backend maps it to a
    /// concrete UI Toolkit / uGUI element on generation.
    /// </summary>
    [Serializable]
    public sealed class UIRecipeElement
    {
        public string elementId;
        public string elementType;
        public string parentElementId;
        public List<string> capabilities = new();
        public List<string> bindingCandidates = new();
    }
}
