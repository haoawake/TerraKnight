using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight.UI
{
    // Custom labels do not give Unity a focused TextField. Own IME activation explicitly,
    // and consume committed text from exactly one backend to avoid duplicate Chinese input.
    internal sealed class ImeTextEntry
    {
        readonly Action<char> append;
        static ImeTextEntry owner;
        Keyboard keyboard;
        bool legacyAvailable;
        bool imeApplied, wasFocused;
        Vector2? cursor;
        IMECompositionMode oldMode;
        int readFrame = -100, imeFrame = -100, compositionEventFrame = -100;
        public bool Active { get; private set; }
        public string Composition { get; private set; } = "";
        public bool EditingComposition => Composition.Length > 0 || Time.frameCount - imeFrame <= 1;
        public string Backend => keyboard != null ? "Input System" : legacyAvailable ? "旧输入" : "无键盘";

        public ImeTextEntry(Action<char> append) { this.append = append; }

        public void Begin()
        {
            if (Active) { Ensure(); return; }
            owner?.End();
            owner = this;
            Active = true;
            Composition = "";
            imeFrame = -100;
            compositionEventFrame = -100;
            readFrame = Time.frameCount;
            imeApplied=false;wasFocused=Application.isFocused;cursor=null;
            try { oldMode = Input.imeCompositionMode; legacyAvailable = true; }
            catch (InvalidOperationException) { legacyAvailable = false; }
            Ensure();
        }

        void Ensure()
        {
            if (!Active) return;
            var current = Keyboard.current;
            if (keyboard != current)
            {
                Detach();
                keyboard = current;
                imeApplied=false;cursor=null;
                if (keyboard != null)
                {
                    keyboard.onTextInput += Text;
                    keyboard.onIMECompositionChange += Composing;
                }
            }
            bool focused=Application.isFocused;
            if(focused&&!wasFocused)imeApplied=false;
            wasFocused=focused;
            if(imeApplied)return;
            // Native IME commands are state changes, not draw commands.
            if (legacyAvailable)
                try { Input.imeCompositionMode = IMECompositionMode.On; }
                catch (InvalidOperationException) { legacyAvailable = false; }
            keyboard?.SetIMEEnabled(true);
            imeApplied=true;
        }

        void Text(char c)
        {
            if (!Active || char.IsControl(c)) return;
            if (Composition.Length > 0 || c > 127) imeFrame = Time.frameCount;
            append(c);
        }

        void Composing(IMECompositionString value)
        {
            if (!Active) return;
            compositionEventFrame = Time.frameCount;
            SetComposition(value.ToString());
        }

        void SetComposition(string value)
        {
            value = value ?? "";
            if (Composition.Length > 0 || value.Length > 0) imeFrame = Time.frameCount;
            Composition = value;
        }

        public void Tick()
        {
            if (!Active) return;
            Ensure();
            if (!legacyAvailable || readFrame == Time.frameCount) return;
            readFrame = Time.frameCount;
            try
            {
                // The old input's composition only until the keyboard has sent one of its own: after that the keyboard's
                // (cleared when a word is committed) is the one, and an old one left over can't hold the typing as
                // "still composing", Backspace and Enter with it.
                if (keyboard == null || compositionEventFrame < 0)
                    SetComposition(Input.compositionString);
                // Keyboard text events are primary. Legacy text is only a fallback when
                // that device is absent, never a second stream of the same committed word.
                if (keyboard == null) foreach (char c in Input.inputString) Text(c);
            }
            catch (InvalidOperationException) { legacyAvailable = false; }
        }

        public void Anchor(Vector2 point)
        {
            if (!Active) return;
            Ensure();
            if(cursor.HasValue&&cursor.Value==point)return;
            cursor=point;
            if (legacyAvailable)
                try { Input.compositionCursorPos = point; }
                catch (InvalidOperationException) { legacyAvailable = false; }
            keyboard?.SetIMECursorPosition(point);
        }

        void Detach()
        {
            if (keyboard == null) return;
            keyboard.onTextInput -= Text;
            keyboard.onIMECompositionChange -= Composing;
            keyboard.SetIMEEnabled(false);
            keyboard = null;
        }

        public void End()
        {
            if (!Active) return;
            Active = false;
            Detach();
            Composition = "";
            if (owner != this) return;
            owner = null;
            if (legacyAvailable)
                try { Input.imeCompositionMode = oldMode; }
                catch (InvalidOperationException) { }
        }

        float nextRepeat = -1f;
        int guiBacks;

        /// <summary>
        /// Each IMGUI event (TerraMod.OnGUI): the window's own Backspace key messages for the entry being typed in, its
        /// repeats too, for when the Input System never sees the key (an input method holding it: the user's 10-25 "起名…
        /// 不能删字").
        /// </summary>
        public static void GuiEvent(Event e)
        {
            var o = owner;
            if (o == null || !o.Active || e == null || e.type != EventType.KeyDown || e.keyCode != KeyCode.Backspace) return;
            o.guiBacks++;
            e.Use();
        }

        /// <summary>
        /// Once a frame: how many characters Backspace takes off (Main.GetInputText: one a press; held, after a moment
        /// one every few ticks). The Input System's key; failing that, the window's key messages and the old input's.
        /// </summary>
        public int Backspaces()
        {
            if (!Active) return 0;
            var kb = keyboard ?? Keyboard.current;
            bool held = kb != null && kb.backspaceKey.isPressed;
            float now = Time.unscaledTime;
            int n = 0;
            if (kb != null && kb.backspaceKey.wasPressedThisFrame) { n = 1; nextRepeat = now + 0.45f; }
            else if (held && nextRepeat >= 0f && now >= nextRepeat) { n = 1; nextRepeat = now + 0.05f; }
            if (!held && n == 0)
            {
                nextRepeat = -1f;
                n = guiBacks;
                if (n == 0 && legacyAvailable)
                    try { if (Input.GetKeyDown(KeyCode.Backspace)) n = 1; }
                    catch (InvalidOperationException) { legacyAvailable = false; }
            }
            guiBacks = 0;
            return n;
        }

        /// <summary>The text with <see cref="Backspaces"/> characters taken off its end.</summary>
        public string TakeBackspaces(string text)
        {
            for (int n = Backspaces(); n > 0 && !string.IsNullOrEmpty(text); n--) text = Backspace(text);
            return text;
        }

        public static string Backspace(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var elements = System.Globalization.StringInfo.ParseCombiningCharacters(text);
            return text.Substring(0, elements[elements.Length - 1]);
        }
    }
}
