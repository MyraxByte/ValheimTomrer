using UnityEngine;
using ValheimTomrer.Editor.Input;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// Quick add: Tab opens the piece palette as a popup over the view, with the keyboard already
    /// in its search box. A click on a tile puts that piece in hand and closes the popup. It
    /// replaces the old Pieces tab, so the left side of the screen belongs to the view. The pad has
    /// the piece menu (<see cref="PiecePicker"/>) for the same job.
    ///
    /// The popup's objects are built by <see cref="EditorWindow"/>. Open means the popup's dim layer
    /// is active; closed means it is switched off. The palette inside only ticks while it is open,
    /// because it measures text and a switched-off object measures nothing.
    /// </summary>
    internal static class QuickAdd
    {
        public static bool IsOpen => EditorWindow.PopupHost != null && EditorWindow.PopupHost.activeSelf;

        /// <summary>Opens the popup. False when it cannot be: no window, a dialog up, or the walk's dialog.</summary>
        public static bool Open()
        {
            var host = EditorWindow.PopupHost;
            if (!ModUi.Open || host == null || IsOpen || Dialogs.IsOpen)
            {
                return IsOpen;
            }

            FocusNav.Leave();
            PiecePicker.Close();
            Fit();
            host.SetActive(true);
            Palette.Reveal();
            Palette.FocusSearch();
            return true;
        }

        /// <summary>Closes the popup. True when it was open.</summary>
        public static bool Close()
        {
            var host = EditorWindow.PopupHost;
            if (host == null || !host.activeSelf)
            {
                return false;
            }

            Palette.HideCard();
            if (ModUi.Typing)
            {
                FocusNav.StopTyping();
            }

            host.SetActive(false);
            return true;
        }

        public static void Toggle()
        {
            if (!Close())
            {
                Open();
            }
        }

        /// <summary>
        /// Once a frame. Tab closes the popup from the search box too: a typing box owns the
        /// keyboard, so the key dispatcher never sees that Tab.
        /// </summary>
        public static void Tick()
        {
            if (IsOpen && ModUi.Typing && Keymap.TypedPressed(Act.QuickAdd))
            {
                Close();
            }
        }

        /// <summary>The popup as large as it likes, but never wider or taller than the screen less a margin.</summary>
        private static void Fit()
        {
            var popup = EditorWindow.Popup;
            var root = EditorWindow.Root;
            if (popup == null || root == null)
            {
                return;
            }

            var width = Mathf.Min(760f, root.rect.width - 80f);
            var height = Mathf.Min(600f, root.rect.height - 140f);
            popup.sizeDelta = new Vector2(Mathf.Max(360f, width), Mathf.Max(320f, height));
        }
    }
}
