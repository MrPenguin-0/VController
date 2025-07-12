using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Tulpep.NotificationWindow;
using System.Timers;
using System.Diagnostics;
using System.IO;

namespace VController
{
    public partial class VController : Form
    {
        private Xbox360Button mappedDPadUpButton = Xbox360Button.Up;
        private Keys? mappedKeyForDPadUp = null;
        private int? mappedMouseForDPadUp = null; // 0 = Left Click, 1 = Right Click

        private Xbox360Button mappedLBButton = Xbox360Button.LeftShoulder;
        private Keys? mappedKeyForLB = null;
        private int? mappedMouseForLB = null; // 0 = Left Click, 1 = Right Click

        private Xbox360Button mappedDPadLeftButton = Xbox360Button.Left;
        private Keys? mappedKeyForDPadLeft = null;
        private int? mappedMouseForDPadLeft = null; // 0 = LC, 1 = RC

        private Xbox360Button mappedDPadDownButton = Xbox360Button.Down;
        private Keys? mappedKeyForDPadDown = null;
        private int? mappedMouseForDPadDown = null; // 0 = LC, 1 = RC

        private Xbox360Button mappedDPadRightButton = Xbox360Button.Right;
        private Keys? mappedKeyForDPadRight = null;
        private int? mappedMouseForDPadRight = null; // 0 = LC, 1 = RC

        private Xbox360Button mappedRightStickButton = Xbox360Button.RightThumb;
        private Keys? mappedKeyForRightStick = null;
        private int? mappedMouseForRightStick = null;

        private Xbox360Button mappedAButton = Xbox360Button.A;
        private Keys? mappedKeyForA = null;
        private int? mappedMouseForA = null;

        private Xbox360Button mappedBButton = Xbox360Button.B;
        private Keys? mappedKeyForB = null;
        private int? mappedMouseForB = null; // 0 = LC, 1 = RC

        private Xbox360Button mappedYButton = Xbox360Button.Y;
        private Keys? mappedKeyForY = null;
        private int? mappedMouseForY = null; // 0 = LC, 1 = RC

        private Xbox360Button mappedXButton = Xbox360Button.X;
        private Keys? mappedKeyForX = null;
        private int? mappedMouseForX = null; // 0 = LC, 1 = RC

        private Xbox360Button mappedRBButton = Xbox360Button.RightShoulder;
        private Keys? mappedKeyForRB = null;
        private int? mappedMouseForRB = null; // 0 = LC, 1 = RC

        private Keys? mappedKeyForRT = null;
        private int? mappedMouseForRT = null; // 0 = LC, 1 = RC

        private Keys? mappedKeyForLT = null;
        private int? mappedMouseForLT = null; // 0 = LC, 1 = RC

        private Xbox360Button mappedStartButton = Xbox360Button.Start;
        private Keys? mappedKeyForStart = null;
        private int? mappedMouseForStart = null; // 0 = LC, 1 = RC

        private Xbox360Button mappedSelectButton = Xbox360Button.Back;
        private Keys? mappedKeyForSelect = null;
        private int? mappedMouseForSelect = null; // 0 = LC, 1 = RC

        private const int RIDEV_INPUTSINK = 0x00000100;
        private const int RID_INPUT = 0x10000003;
        private const int RIM_TYPEMOUSE = 0;

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public uint dwType;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct RAWMOUSE
        {
            [FieldOffset(0)]
            public ushort usFlags;
            [FieldOffset(4)]
            public uint ulButtons;
            [FieldOffset(4)]
            public ushort usButtonFlags;
            [FieldOffset(6)]
            public ushort usButtonData;
            [FieldOffset(8)]
            public uint ulRawButtons;
            [FieldOffset(12)]
            public int lLastX;
            [FieldOffset(16)]
            public int lLastY;
            [FieldOffset(20)]
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWMOUSE mouse;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private const int HOTKEY_ID_HOME = 9001;

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private float smoothedRX = 0f, smoothedRY = 0f;

        // Sensibilité revue à la baisse pour éviter saturation et trop de mouvements
        private const float mouseSensitivityX = 200f;
        private const float mouseSensitivityY = 200f;

        private const float maxAnalog = 32767f;
        private const float deadzoneThreshold = 40f;

        private ViGEmClient client;
        private IXbox360Controller controller;
        private System.Timers.Timer inputTimer;
        private bool isRunning = false;
        private bool isPaused = false;

        public Point mouseLocation;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);

        public VController()
        {
            InitializeComponent();

            this.FormBorderStyle = FormBorderStyle.None;
            Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
            this.StartPosition = FormStartPosition.CenterScreen;

            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            textBox3.TextChanged += textBox3_TextChanged;
            textBox9.TextChanged += textBox9_TextChanged;
            textBox10.TextChanged += textBox10_TextChanged;
            textBox13.TextChanged += textBox13_TextChanged;
            textBox20.TextChanged += textBox20_TextChanged;
            textBox21.TextChanged += textBox21_TextChanged;
            textBox19.TextChanged += textBox19_TextChanged;
            textBox17.TextChanged += textBox17_TextChanged;
            textBox16.TextChanged += textBox16_TextChanged;
            textBox14.TextChanged += textBox14_TextChanged;
            textBox4.TextChanged += textBox4_TextChanged;
            textBox2.TextChanged += textBox2_TextChanged;
            textBox1.TextChanged += textBox1_TextChanged;
            textBox11.TextChanged += textBox11_TextChanged;
            textBox12.TextChanged += textBox12_TextChanged;
        }
        private void UpdateRBKeyMapping()
        {
            string input = textBox4.Text.Trim().ToLower();

            mappedKeyForRB = null;
            mappedMouseForRB = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForRB = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForRB = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForRB = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForRB = 0;
                    break;
                case "rc":
                    mappedMouseForRB = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                        mappedKeyForRB = key;
                    break;
            }
        }
        private void UpdateStartKeyMapping()
        {
            string input = textBox11.Text.Trim().ToLower();

            mappedKeyForStart = null;
            mappedMouseForStart = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForStart = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForStart = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForStart = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForStart = 0;
                    break;
                case "rc":
                    mappedMouseForStart = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                        mappedKeyForStart = key;
                    break;
            }
        }
        private void UpdateSelectKeyMapping()
        {
            string input = textBox12.Text.Trim().ToLower();

            mappedKeyForSelect = null;
            mappedMouseForSelect = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForSelect = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForSelect = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForSelect = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForSelect = 0;
                    break;
                case "rc":
                    mappedMouseForSelect = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                        mappedKeyForSelect = key;
                    break;
            }
        }

        private void UpdateRTKeyMapping()
        {
            string input = textBox2.Text.Trim().ToLower();

            mappedKeyForRT = null;
            mappedMouseForRT = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForRT = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForRT = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForRT = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForRT = 0;
                    break;
                case "rc":
                    mappedMouseForRT = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                        mappedKeyForRT = key;
                    break;
            }
        }

        private void UpdateLTKeyMapping()
        {
            string input = textBox1.Text.Trim().ToLower();

            mappedKeyForLT = null;
            mappedMouseForLT = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForLT = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForLT = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForLT = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForLT = 0;
                    break;
                case "rc":
                    mappedMouseForLT = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                        mappedKeyForLT = key;
                    break;
            }
        }

        private void UpdateYButtonKeyMapping()
        {
            string input = textBox16.Text.Trim().ToLower();

            mappedKeyForY = null;
            mappedMouseForY = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForY = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForY = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForY = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForY = 0;
                    break;
                case "rc":
                    mappedMouseForY = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                        mappedKeyForY = key;
                    break;
            }
        }
        private void UpdateXButtonKeyMapping()
        {
            string input = textBox14.Text.Trim().ToLower();

            mappedKeyForX = null;
            mappedMouseForX = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForX = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForX = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForX = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForX = 0;
                    break;
                case "rc":
                    mappedMouseForX = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                        mappedKeyForX = key;
                    break;
            }
        }


        private void UpdateBButtonKeyMapping()
        {
            string input = textBox17.Text.Trim().ToLower();

            mappedKeyForB = null;
            mappedMouseForB = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForB = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForB = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForB = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForB = 0;
                    break;
                case "rc":
                    mappedMouseForB = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                        mappedKeyForB = key;
                    break;
            }
        }

        private void UpdateDPadUpKeyMapping()
        {
            string input = textBox9.Text.Trim().ToLower();

            mappedKeyForDPadUp = null;
            mappedMouseForDPadUp = null;

            switch (input)
            {
                case "tab": mappedKeyForDPadUp = Keys.Tab; break;
                case "esc": mappedKeyForDPadUp = Keys.Escape; break;
                case "space": mappedKeyForDPadUp = Keys.Space; break;
                case "lc": mappedMouseForDPadUp = 0; break; // Left click
                case "rc": mappedMouseForDPadUp = 1; break; // Right click
                default:
                    if (Enum.TryParse(input, true, out Keys result))
                        mappedKeyForDPadUp = result;
                    break;
            }
        }
        private void UpdateAButtonKeyMapping()
        {
            string input = textBox19.Text.Trim().ToLower();

            mappedKeyForA = null;
            mappedMouseForA = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForA = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForA = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForA = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForA = 0;
                    break;
                case "rc":
                    mappedMouseForA = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys result))
                        mappedKeyForA = result;
                    break;
            }
        }

        private void UpdateRightStickKeyMapping()
        {
            string input = textBox21.Text.Trim().ToLower();

            mappedKeyForRightStick = null;
            mappedMouseForRightStick = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForRightStick = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForRightStick = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForRightStick = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForRightStick = 0;
                    break;
                case "rc":
                    mappedMouseForRightStick = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                        mappedKeyForRightStick = key;
                    break;
            }
        }

        private void UpdateDPadRightKeyMapping()
        {
            string input = textBox20.Text.Trim().ToLower();

            mappedKeyForDPadRight = null;
            mappedMouseForDPadRight = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForDPadRight = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForDPadRight = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForDPadRight = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForDPadRight = 0;
                    break;
                case "rc":
                    mappedMouseForDPadRight = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys result))
                        mappedKeyForDPadRight = result;
                    break;
            }
        }

        private void UpdateDPadDownKeyMapping()
        {
            string input = textBox13.Text.Trim().ToLower();

            mappedKeyForDPadDown = null;
            mappedMouseForDPadDown = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForDPadDown = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForDPadDown = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForDPadDown = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForDPadDown = 0; // Left click
                    break;
                case "rc":
                    mappedMouseForDPadDown = 1; // Right click
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys result))
                        mappedKeyForDPadDown = result;
                    break;
            }
        }


        private void UpdateDPadLeftKeyMapping()
        {
            string input = textBox10.Text.Trim().ToLower();

            mappedKeyForDPadLeft = null;
            mappedMouseForDPadLeft = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForDPadLeft = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForDPadLeft = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForDPadLeft = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForDPadLeft = 0;
                    break;
                case "rc":
                    mappedMouseForDPadLeft = 1;
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys result))
                        mappedKeyForDPadLeft = result;
                    break;
            }
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01;  // Generic desktop controls
            rid[0].usUsage = 0x02;      // Mouse
            rid[0].dwFlags = RIDEV_INPUTSINK;
            rid[0].hwndTarget = this.Handle;
            RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));

            pictureBox27.Visible = false;
            await Task.Delay(1000);
            pictureBox27.Visible = true;
            await Task.Delay(1000);
            panel27.Visible = false;

            panel25.Visible = Properties.Settings.Default.ShowPanel25Message;

            RegisterHotKey(this.Handle, HOTKEY_ID, 0, Keys.F6);
            RegisterHotKey(this.Handle, HOTKEY_ID_HOME, 0, Keys.Home);

            UpdateLBKeyMapping();
            UpdateDPadUpKeyMapping();
            UpdateDPadLeftKeyMapping();
            UpdateDPadDownKeyMapping();
            UpdateDPadRightKeyMapping();
            UpdateRightStickKeyMapping();
            UpdateAButtonKeyMapping();
            UpdateBButtonKeyMapping();
            UpdateYButtonKeyMapping();
            UpdateXButtonKeyMapping();
            UpdateRBKeyMapping();
            UpdateRTKeyMapping();
            UpdateLTKeyMapping();
            UpdateStartKeyMapping();
            UpdateSelectKeyMapping();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_INPUT = 0x00FF;
            const int WM_HOTKEY = 0x0312;

            if (m.Msg == WM_INPUT && isRunning && !isPaused)
            {
                uint dwSize = 0;
                GetRawInputData(m.LParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                GetRawInputData(m.LParam, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                if (raw.header.dwType == RIM_TYPEMOUSE)
                {
                    int deltaX = raw.mouse.lLastX;
                    int deltaY = raw.mouse.lLastY;

                    // Calcul linéaire avec sensibilité adaptée
                    float targetRX = deltaX * mouseSensitivityX;
                    float targetRY = -deltaY * mouseSensitivityY;

                    // Smoothing adaptatif : plus le mouvement est grand, plus on réagit vite
                    float magnitude = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    float smoothingFactor = Clamp(0.05f + (magnitude / 20f), 0.05f, 0.3f);

                    smoothedRX += (targetRX - smoothedRX) * smoothingFactor;
                    smoothedRY += (targetRY - smoothedRY) * smoothingFactor;

                    // Deadzone avec interpolation douce vers zéro
                    smoothedRX = ApplyDeadzoneWithLerp(smoothedRX, deadzoneThreshold);
                    smoothedRY = ApplyDeadzoneWithLerp(smoothedRY, deadzoneThreshold);

                    short outputRX = (short)Clamp(smoothedRX, -maxAnalog, maxAnalog);
                    short outputRY = (short)Clamp(smoothedRY, -maxAnalog, maxAnalog);

                    controller?.SetAxisValue(Xbox360Axis.RightThumbX, outputRX);
                    controller?.SetAxisValue(Xbox360Axis.RightThumbY, outputRY);
                }

                Marshal.FreeHGlobal(buffer);
            }
            else if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_ID)
                    ToggleVirtualController();
                else if (id == HOTKEY_ID_HOME)
                    TogglePause();
            }

            base.WndProc(ref m);
        }

        private float ApplyDeadzoneWithLerp(float value, float deadzone)
        {
            if (Math.Abs(value) < deadzone)
            {
                // Interpolation douce vers 0 (décroissance progressive)
                value *= 0.75f;
                if (Math.Abs(value) < 0.1f)
                    value = 0f;
            }
            return value;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            UnregisterHotKey(this.Handle, HOTKEY_ID_HOME);
            base.OnFormClosing(e);
        }

        private void UpdateLBKeyMapping()
        {
            string input = textBox3.Text.Trim().ToLower();

            mappedKeyForLB = null;
            mappedMouseForLB = null;

            switch (input)
            {
                case "tab":
                    mappedKeyForLB = Keys.Tab;
                    break;
                case "esc":
                    mappedKeyForLB = Keys.Escape;
                    break;
                case "space":
                    mappedKeyForLB = Keys.Space;
                    break;
                case "lc":
                    mappedMouseForLB = 0; // Left click
                    break;
                case "rc":
                    mappedMouseForLB = 1; // Right click
                    break;
                default:
                    if (Enum.TryParse(input, true, out Keys key))
                    {
                        mappedKeyForLB = key;
                    }
                    break;
            }
        }


        private void ShowCustomNotification(string title, string message)
        {
            var popup = new PopupNotifier
            {
                TitleText = title,
                ContentText = message,
                Delay = 3000,
                AnimationDuration = 500,
                ShowCloseButton = false,
                BodyColor = Color.FromArgb(255, 24, 24, 24),
                BorderColor = Color.White,
                Image = null,
                TitleFont = new Font("Segoe UI", 12F, FontStyle.Bold),
                TitleColor = Color.White,
                ContentFont = new Font("Segoe UI", 10F, FontStyle.Regular),
                ContentColor = Color.LightGray,
            };

            popup.Popup();
        }

        private void ToggleVirtualController()
        {
            if (!isRunning)
            {
                if (client == null)
                    client = new ViGEmClient();

                if (controller == null)
                    controller = client.CreateXbox360Controller();

                controller.Connect();

                inputTimer = new System.Timers.Timer(5); // Timer à 5 ms au lieu de 1 ms
                inputTimer.Elapsed += InputTimer_Tick;
                inputTimer.AutoReset = true;
                inputTimer.Start();

                isRunning = true;
                isPaused = false;
                startButton.Text = "STOP (F6)";
                Cursor.Hide();

                if (checkBox2.Checked)
                    ShowCustomNotification("Xbox 360 Controller for Windows", "Connected");
            }
            else
            {
                inputTimer?.Stop();
                inputTimer?.Dispose();
                inputTimer = null;

                controller?.Disconnect();
                controller = null;

                isRunning = false;
                startButton.Text = "START (F6)";
                Cursor.Show();

                if (checkBox2.Checked)
                    ShowCustomNotification("Xbox 360 Controller for Windows", "Disconnected");
            }
        }

        private void TogglePause()
        {
            if (!isRunning) return;

            isPaused = !isPaused;

            if (isPaused)
            {
                startButton.Text = "PAUSED";
                Cursor.Show();
            }
            else
            {
                startButton.Text = "STOP (F6)";
                Cursor.Hide();
            }
        }

        private float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
        private void InputTimer_Tick(object sender, ElapsedEventArgs e)
        {
            if (isPaused) return;

            short lx = 0, ly = 0;
            if (GetAsyncKeyState(Keys.W) < 0) ly += 32767;
            if (GetAsyncKeyState(Keys.S) < 0) ly -= 32767;
            if (GetAsyncKeyState(Keys.A) < 0) lx -= 32767;
            if (GetAsyncKeyState(Keys.D) < 0) lx += 32767;

            controller?.SetAxisValue(Xbox360Axis.LeftThumbX, lx);
            controller?.SetAxisValue(Xbox360Axis.LeftThumbY, ly);

            // 👇 Check for LB emulation from key/mouse
            bool lbPressed = false;

            if (mappedKeyForLB.HasValue && GetAsyncKeyState(mappedKeyForLB.Value) < 0)
                lbPressed = true;
            else if (mappedMouseForLB == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                lbPressed = true;
            else if (mappedMouseForLB == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                lbPressed = true;

            if (lbPressed)
                controller?.SetButtonState(mappedLBButton, true);
            else
                controller?.SetButtonState(mappedLBButton, false);
            // 👇 Check for DPad_Up emulation from key/mouse
            bool dpadUpPressed = false;

            if (mappedKeyForDPadUp.HasValue && GetAsyncKeyState(mappedKeyForDPadUp.Value) < 0)
                dpadUpPressed = true;
            else if (mappedMouseForDPadUp == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                dpadUpPressed = true;
            else if (mappedMouseForDPadUp == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                dpadUpPressed = true;

            controller?.SetButtonState(mappedDPadUpButton, dpadUpPressed);
            // 👇 Check for DPad_Left emulation from key/mouse
            bool dpadLeftPressed = false;

            if (mappedKeyForDPadLeft.HasValue && GetAsyncKeyState(mappedKeyForDPadLeft.Value) < 0)
                dpadLeftPressed = true;
            else if (mappedMouseForDPadLeft == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                dpadLeftPressed = true;
            else if (mappedMouseForDPadLeft == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                dpadLeftPressed = true;

            controller?.SetButtonState(mappedDPadLeftButton, dpadLeftPressed);
            // 👇 Check for DPad_Down emulation from key/mouse
            bool dpadDownPressed = false;

            if (mappedKeyForDPadDown.HasValue && GetAsyncKeyState(mappedKeyForDPadDown.Value) < 0)
                dpadDownPressed = true;
            else if (mappedMouseForDPadDown == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                dpadDownPressed = true;
            else if (mappedMouseForDPadDown == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                dpadDownPressed = true;

            controller?.SetButtonState(mappedDPadDownButton, dpadDownPressed);
            // 👇 Check for DPad_Right emulation from key/mouse
            bool dpadRightPressed = false;

            if (mappedKeyForDPadRight.HasValue && GetAsyncKeyState(mappedKeyForDPadRight.Value) < 0)
                dpadRightPressed = true;
            else if (mappedMouseForDPadRight == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                dpadRightPressed = true;
            else if (mappedMouseForDPadRight == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                dpadRightPressed = true;

            controller?.SetButtonState(mappedDPadRightButton, dpadRightPressed);
            // 👇 Check for RightStick emulation from key/mouse
            bool rightStickPressed = false;

            if (mappedKeyForRightStick.HasValue && GetAsyncKeyState(mappedKeyForRightStick.Value) < 0)
                rightStickPressed = true;
            else if (mappedMouseForRightStick == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                rightStickPressed = true;
            else if (mappedMouseForRightStick == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                rightStickPressed = true;

            controller?.SetButtonState(mappedRightStickButton, rightStickPressed);
            // 👇 Check for A button emulation from key/mouse
            bool aPressed = false;

            if (mappedKeyForA.HasValue && GetAsyncKeyState(mappedKeyForA.Value) < 0)
                aPressed = true;
            else if (mappedMouseForA == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                aPressed = true;
            else if (mappedMouseForA == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                aPressed = true;

            controller?.SetButtonState(mappedAButton, aPressed);
            // 👇 Check for B button emulation from key/mouse
            bool bPressed = false;

            if (mappedKeyForB.HasValue && GetAsyncKeyState(mappedKeyForB.Value) < 0)
                bPressed = true;
            else if (mappedMouseForB == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                bPressed = true;
            else if (mappedMouseForB == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                bPressed = true;

            controller?.SetButtonState(mappedBButton, bPressed);
            // 👇 Check for Y button emulation from key/mouse
            bool yPressed = false;

            if (mappedKeyForY.HasValue && GetAsyncKeyState(mappedKeyForY.Value) < 0)
                yPressed = true;
            else if (mappedMouseForY == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                yPressed = true;
            else if (mappedMouseForY == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                yPressed = true;

            controller?.SetButtonState(mappedYButton, yPressed);
            // 👇 Check for X button emulation from key/mouse
            bool xPressed = false;

            if (mappedKeyForX.HasValue && GetAsyncKeyState(mappedKeyForX.Value) < 0)
                xPressed = true;
            else if (mappedMouseForX == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                xPressed = true;
            else if (mappedMouseForX == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                xPressed = true;

            controller?.SetButtonState(mappedXButton, xPressed);
            // 👇 Check for RB emulation from key/mouse
            bool rbPressed = false;

            if (mappedKeyForRB.HasValue && GetAsyncKeyState(mappedKeyForRB.Value) < 0)
                rbPressed = true;
            else if (mappedMouseForRB == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                rbPressed = true;
            else if (mappedMouseForRB == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                rbPressed = true;

            controller?.SetButtonState(mappedRBButton, rbPressed);
            // 👇 Check for RT emulation from key/mouse
            byte rtValue = 0;

            if (mappedKeyForRT.HasValue && GetAsyncKeyState(mappedKeyForRT.Value) < 0)
                rtValue = 255;
            else if (mappedMouseForRT == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                rtValue = 255;
            else if (mappedMouseForRT == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                rtValue = 255;

            controller?.SetSliderValue(Xbox360Slider.RightTrigger, rtValue);
            // 👇 Check for LT emulation from key/mouse
            byte ltValue = 0;

            if (mappedKeyForLT.HasValue && GetAsyncKeyState(mappedKeyForLT.Value) < 0)
                ltValue = 255;
            else if (mappedMouseForLT == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                ltValue = 255;
            else if (mappedMouseForLT == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                ltValue = 255;

            controller?.SetSliderValue(Xbox360Slider.LeftTrigger, ltValue);
            // 👇 Check for START emulation from key/mouse
            bool startPressed = false;

            if (mappedKeyForStart.HasValue && GetAsyncKeyState(mappedKeyForStart.Value) < 0)
                startPressed = true;
            else if (mappedMouseForStart == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                startPressed = true;
            else if (mappedMouseForStart == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                startPressed = true;

            controller?.SetButtonState(mappedStartButton, startPressed);
            // 👇 Check for SELECT/BACK emulation from key/mouse
            bool selectPressed = false;

            if (mappedKeyForSelect.HasValue && GetAsyncKeyState(mappedKeyForSelect.Value) < 0)
                selectPressed = true;
            else if (mappedMouseForSelect == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                selectPressed = true;
            else if (mappedMouseForSelect == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                selectPressed = true;

            controller?.SetButtonState(mappedSelectButton, selectPressed);
        }


        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F6)
                ToggleVirtualController();
            else if (e.KeyCode == Keys.Home)
                TogglePause();
        }

        private void button1_Click(object sender, EventArgs e) => ToggleVirtualController();
        private void button1_Click_1(object sender, EventArgs e) => Application.Exit();
        private void button6_Click(object sender, EventArgs e) => this.WindowState = FormWindowState.Minimized;
        private void mouse_Down(object sender, MouseEventArgs e) => mouseLocation = new Point(-e.X, -e.Y);
        private void mouse_Move(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point mousePose = Control.MousePosition;
                mousePose.Offset(mouseLocation.X, mouseLocation.Y);
                Location = mousePose;
            }
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            double currentY = 562;
            while (currentY > -2)
            {
                currentY -= 50;
                panel23.Location = new Point(-5, (int)Math.Round(currentY));
                await Task.Delay(1);
            }
            panel23.Location = new Point(-5, -2);
        }

        private async void button7_Click(object sender, EventArgs e)
        {
            double currentY = -2;
            while (currentY < 562)
            {
                currentY += 50;
                panel23.Location = new Point(-5, (int)Math.Round(currentY));
                await Task.Delay(1);
            }
            panel23.Location = new Point(-5, 562);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (!checkBox1.Checked)
            {
                checkBox2.Checked = false;
                checkBox5.Checked = false;
                checkBox8.Checked = false;
            }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox5.Checked)
                ShowCustomNotification("Sticky Aim", checkBox3.Checked ? "Activated" : "Deactivated");
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox8.Checked)
                ShowCustomNotification("Anti-Recoil", checkBox4.Checked ? "Activated" : "Deactivated");
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (checkBox6.Checked)
            {
                Properties.Settings.Default.ShowPanel25Message = false;
                Properties.Settings.Default.Save();
            }
            panel25.Visible = false;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.Title = "Import TextBox Data";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string[] lines = File.ReadAllLines(openFileDialog.FileName);

                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            string name = parts[0].Trim();
                            string value = parts[1].Trim();

                            Control[] controls = this.Controls.Find(name, true);
                            if (controls.Length > 0 && controls[0] is TextBox tb)
                            {
                                tb.Text = value;
                            }
                        }
                    }
                }
            }
        }



        private void button3_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                saveFileDialog.Title = "Export TextBox Data";
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.AddExtension = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine($"textBox1={textBox1.Text}");
                    sb.AppendLine($"textBox3={textBox3.Text}");
                    sb.AppendLine($"textBox12={textBox12.Text}");
                    sb.AppendLine($"textBox11={textBox11.Text}");
                    sb.AppendLine($"textBox14={textBox14.Text}");
                    sb.AppendLine($"textBox2={textBox2.Text}");
                    sb.AppendLine($"textBox4={textBox4.Text}");
                    sb.AppendLine($"textBox16={textBox16.Text}");
                    sb.AppendLine($"textBox17={textBox17.Text}");
                    sb.AppendLine($"textBox19={textBox19.Text}");
                    sb.AppendLine($"textBox21={textBox21.Text}");
                    sb.AppendLine($"textBox20={textBox20.Text}");
                    sb.AppendLine($"textBox13={textBox13.Text}");
                    sb.AppendLine($"textBox10={textBox10.Text}");
                    sb.AppendLine($"textBox9={textBox9.Text}");

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString());
                }
            }
        }


        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            //Nothing
        }

        private void panel12_Paint(object sender, PaintEventArgs e) { }
        private void panel23_Paint_1(object sender, PaintEventArgs e) { }
        private void checkBox2_CheckedChanged(object sender, EventArgs e) { }
        private void checkBox5_CheckedChanged(object sender, EventArgs e) { }
        private void checkBox6_CheckedChanged(object sender, EventArgs e) { }
        private void checkBox8_CheckedChanged(object sender, EventArgs e) { }
        private void panel27_Paint(object sender, PaintEventArgs e) { }
        private void label1_Click(object sender, EventArgs e) { }
        private void pictureBox27_Click(object sender, EventArgs e) { }
        private void pictureBox18_Click(object sender, EventArgs e) { }
        private void textBox17_TextChanged(object sender, EventArgs e)
        {
            UpdateBButtonKeyMapping();
        }

        private void textBox16_TextChanged(object sender, EventArgs e) { }
        private void textBox19_TextChanged(object sender, EventArgs e)
        {
            UpdateAButtonKeyMapping();
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            UpdateLBKeyMapping();
        }

        private void textBox9_TextChanged(object sender, EventArgs e)
        {
            UpdateDPadUpKeyMapping();
        }

        private void textBox10_TextChanged(object sender, EventArgs e)
        {
            UpdateDPadLeftKeyMapping();
        }

        private void textBox13_TextChanged(object sender, EventArgs e)
        {
            UpdateDPadDownKeyMapping();
        }

        private void textBox20_TextChanged(object sender, EventArgs e)
        {
            UpdateDPadRightKeyMapping();
        }

        private void textBox21_TextChanged(object sender, EventArgs e)
        {
            UpdateRightStickKeyMapping();
        }

        private void textBox14_TextChanged(object sender, EventArgs e)
        {
            UpdateXButtonKeyMapping();
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {
            UpdateRBKeyMapping();
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            UpdateRTKeyMapping();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            UpdateLTKeyMapping();
        }

        private void textBox11_TextChanged(object sender, EventArgs e)
        {
            UpdateStartKeyMapping();
        }

        private void textBox12_TextChanged(object sender, EventArgs e)
        {
            UpdateSelectKeyMapping();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            textBox1.Text = "LC";
            textBox3.Text = "G";
            textBox9.Text = "Z";
            textBox10.Text = "T";
            textBox13.Text = "TAB";
            textBox20.Text = "X";
            textBox21.Text = "V";
            textBox19.Text = "SPACE";
            textBox17.Text = "C";
            textBox16.Text = "Q";
            textBox4.Text = "E";
            textBox2.Text = "RC";
            textBox14.Text = "R";
            textBox11.Text = "ESC";
            textBox12.Text = "M";
        }

        private void pictureBox1_Click(object sender, EventArgs e) { }
    }
}
