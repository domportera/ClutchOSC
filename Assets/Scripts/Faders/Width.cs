using UnityEngine;

namespace Faders
{
    internal static class Width
    {
        public static void UpdateWidth(RectTransform rectTransform, ControllerData.WidthArgs args, Vector2 initialSizeDelta)
        {
            var width = args.Width;

            if (!args.Profile.SpanWidth)
            {
                rectTransform.sizeDelta = new Vector2(initialSizeDelta.x * width, initialSizeDelta.y);
            }
            else
            {
                var totalWidthAll = 0f;
                for(int i = 0; i < args.Profile.AllControllers.Count; i++)
                {
                    if (args.Profile.AllControllers[i].Enabled)
                        totalWidthAll += args.Profile.AllControllers[i].Width;
                }
                var percentage = width / totalWidthAll;
                rectTransform.sizeDelta = new Vector2(percentage, initialSizeDelta.y);
            }
        }
    }
}