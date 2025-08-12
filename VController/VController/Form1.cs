using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Tulpep.NotificationWindow;

namespace VController
{
    public partial class VController : Form
    {
        // Add this with your other constants at the top of the class
        private const float macroSensitivityX = 500f; // Much higher sensitivity for the macro
        private bool is360MacroActive = false; // Track if macro is enabled

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
        private bool show360MacroNotification = true; // default to showing notifications

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

        private void SaveSettings()
        {
            Properties.Settings.Default.Sensitivity = sensitivityTrackBar.Value; // Sensitivity value
            Properties.Settings.Default.LB_Key = textBox3.Text;
            Properties.Settings.Default.DPadUp_Key = textBox9.Text;
            Properties.Settings.Default.DPadLeft_Key = textBox10.Text;
            Properties.Settings.Default.DPadDown_Key = textBox13.Text;
            Properties.Settings.Default.DPadRight_Key = textBox20.Text;
            Properties.Settings.Default.RightStick_Key = textBox21.Text;
            Properties.Settings.Default.A_Key = textBox19.Text;
            Properties.Settings.Default.B_Key = textBox17.Text;
            Properties.Settings.Default.Y_Key = textBox16.Text;
            Properties.Settings.Default.X_Key = textBox14.Text;
            Properties.Settings.Default.RB_Key = textBox4.Text;
            Properties.Settings.Default.RT_Key = textBox2.Text;
            Properties.Settings.Default.LT_Key = textBox1.Text;
            Properties.Settings.Default.Start_Key = textBox11.Text;
            Properties.Settings.Default.Select_Key = textBox12.Text;

            Properties.Settings.Default.Save();
        }
        private void UpdateSensitivityFromTrackbar()
        {
            // Map trackbar value (1-10) to sensitivity multiplier (0.5x-2.0x)
            float sensitivityMultiplier = 0.5f + (sensitivityTrackBar.Value - 1) * 0.1667f;

            currentMouseSensitivityX = baseMouseSensitivityX * sensitivityMultiplier;
            currentMouseSensitivityY = baseMouseSensitivityY * sensitivityMultiplier;

            UpdateSensitivityLabel();
        }
        private void LoadSettings()
        {
            // Load sensitivity first
            sensitivityTrackBar.Value = Properties.Settings.Default.Sensitivity;
            UpdateSensitivityFromTrackbar(); // This will set currentMouseSensitivityX/Y based on trackbar value
            textBox3.Text = Properties.Settings.Default.LB_Key;
            textBox9.Text = Properties.Settings.Default.DPadUp_Key;
            textBox10.Text = Properties.Settings.Default.DPadLeft_Key;
            textBox13.Text = Properties.Settings.Default.DPadDown_Key;
            textBox20.Text = Properties.Settings.Default.DPadRight_Key;
            textBox21.Text = Properties.Settings.Default.RightStick_Key;
            textBox19.Text = Properties.Settings.Default.A_Key;
            textBox17.Text = Properties.Settings.Default.B_Key;
            textBox16.Text = Properties.Settings.Default.Y_Key;
            textBox14.Text = Properties.Settings.Default.X_Key;
            textBox4.Text = Properties.Settings.Default.RB_Key;
            textBox2.Text = Properties.Settings.Default.RT_Key;
            textBox1.Text = Properties.Settings.Default.LT_Key;
            textBox11.Text = Properties.Settings.Default.Start_Key;
            textBox12.Text = Properties.Settings.Default.Select_Key;

            // Update all key mappings after loading
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
            // Trackbar setup
            sensitivityTrackBar.Minimum = 1;
            sensitivityTrackBar.Maximum = 10;
            sensitivityTrackBar.Value = 4; // Default value
            sensitivityTrackBar.Scroll += SensitivityTrackBar_Scroll;

            checkBox11.Checked = true; // Linear by default
            currentSmoothingMode = SmoothingMode.Linear;
            AdvancedSettingsPanel.Hide();

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

            // Load saved settings
            LoadSettings();

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

        // Add these class-level variables somewhere in your class (adjust size as needed)
        private bool[] _previousButtonStates = new bool[16]; // Make sure array size matches number of buttons you track
        private short _previousLX = 0, _previousLY = 0;
        private byte _previousRT = 0, _previousLT = 0;

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

                    // Calculate adaptive smoothing based on movement speed
                    float smoothingFactor = CalculateAdaptiveSmoothing(deltaX, deltaY);

                    // Use current sensitivity values instead of constants
                    float targetRX = deltaX * currentMouseSensitivityX;
                    float targetRY = -deltaY * currentMouseSensitivityY;

                    // Apply response curve to target values
                    targetRX = ApplyResponseCurve(targetRX, currentCurveType);
                    targetRY = ApplyResponseCurve(targetRY, currentCurveType);

                    // Apply selected smoothing mode with adaptive factor
                    switch (currentSmoothingMode)
                    {
                        case SmoothingMode.Linear:
                            smoothedRX = LinearInterpolation(smoothedRX, targetRX, smoothingFactor);
                            smoothedRY = LinearInterpolation(smoothedRY, targetRY, smoothingFactor);
                            break;

                        case SmoothingMode.Exponential:
                            smoothedRX = ExponentialSmoothing(smoothedRX, targetRX, smoothingFactor);
                            smoothedRY = ExponentialSmoothing(smoothedRY, targetRY, smoothingFactor);
                            break;
                    }

                    // Apply deadzone with smooth transition
                    smoothedRX = ApplyDeadzoneWithLerp(smoothedRX, deadzoneThreshold);
                    smoothedRY = ApplyDeadzoneWithLerp(smoothedRY, deadzoneThreshold);

                    // Clamp final values
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
        private float GetSmoothingFactor(float magnitude)
        {
            float baseFactor;

            if (currentSmoothingMode == SmoothingMode.Linear)
            {
                baseFactor = 0.2f; // More direct response for linear
            }
            else // Exponential
            {
                baseFactor = 0.1f; // Slower response for exponential
            }

            // Adaptive adjustment based on movement speed
            return Clamp(baseFactor * (1 - (float)Math.Exp(-magnitude / 50f)),
                        minSmoothing,
                        maxSmoothing);
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
            SaveSettings(); // Save settings before closing
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
        // Updated InputTimer_Tick:
        private void InputTimer_Tick(object sender, ElapsedEventArgs e)
        {
            if (isPaused) return;

            // Process left thumbstick input
            short lx = 0, ly = 0;
            if (GetAsyncKeyState(Keys.W) < 0) ly += 32767;
            if (GetAsyncKeyState(Keys.S) < 0) ly -= 32767;
            if (GetAsyncKeyState(Keys.A) < 0) lx -= 32767;
            if (GetAsyncKeyState(Keys.D) < 0) lx += 32767;

            if (lx != _previousLX || ly != _previousLY)
            {
                controller?.SetAxisValue(Xbox360Axis.LeftThumbX, lx);
                controller?.SetAxisValue(Xbox360Axis.LeftThumbY, ly);
                _previousLX = lx;
                _previousLY = ly;
            }

            // Update buttons using helper method and previous states to avoid redundant updates
            UpdateButtonState(mappedLBButton, GetButtonState(mappedKeyForLB, mappedMouseForLB), 0);
            UpdateButtonState(mappedDPadUpButton, GetButtonState(mappedKeyForDPadUp, mappedMouseForDPadUp), 1);
            UpdateButtonState(mappedDPadLeftButton, GetButtonState(mappedKeyForDPadLeft, mappedMouseForDPadLeft), 2);
            UpdateButtonState(mappedDPadDownButton, GetButtonState(mappedKeyForDPadDown, mappedMouseForDPadDown), 3);
            UpdateButtonState(mappedDPadRightButton, GetButtonState(mappedKeyForDPadRight, mappedMouseForDPadRight), 4);
            UpdateButtonState(mappedRightStickButton, GetButtonState(mappedKeyForRightStick, mappedMouseForRightStick), 5);
            UpdateButtonState(mappedAButton, GetButtonState(mappedKeyForA, mappedMouseForA), 6);
            UpdateButtonState(mappedBButton, GetButtonState(mappedKeyForB, mappedMouseForB), 7);
            UpdateButtonState(mappedYButton, GetButtonState(mappedKeyForY, mappedMouseForY), 8);
            UpdateButtonState(mappedXButton, GetButtonState(mappedKeyForX, mappedMouseForX), 9);
            UpdateButtonState(mappedRBButton, GetButtonState(mappedKeyForRB, mappedMouseForRB), 10);
            UpdateButtonState(mappedStartButton, GetButtonState(mappedKeyForStart, mappedMouseForStart), 11);
            UpdateButtonState(mappedSelectButton, GetButtonState(mappedKeyForSelect, mappedMouseForSelect), 12);

            // Process triggers with previous value checks
            byte rtValue = GetTriggerState(mappedKeyForRT, mappedMouseForRT);
            if (rtValue != _previousRT)
            {
                controller?.SetSliderValue(Xbox360Slider.RightTrigger, rtValue);
                _previousRT = rtValue;
            }

            byte ltValue = GetTriggerState(mappedKeyForLT, mappedMouseForLT);
            if (ltValue != _previousLT)
            {
                controller?.SetSliderValue(Xbox360Slider.LeftTrigger, ltValue);
                _previousLT = ltValue;
            }
        }
        // Helper method to get button state (key or mouse)
        private bool GetButtonState(Keys? mappedKey, int? mappedMouse)
        {
            if (mappedKey.HasValue && GetAsyncKeyState(mappedKey.Value) < 0)
                return true;
            if (mappedMouse == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                return true;
            if (mappedMouse == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                return true;
            return false;
        }

        // Helper method to get trigger value (255 if pressed, else 0)
        private byte GetTriggerState(Keys? mappedKey, int? mappedMouse)
        {
            if (mappedKey.HasValue && GetAsyncKeyState(mappedKey.Value) < 0)
                return 255;
            if (mappedMouse == 0 && Control.MouseButtons.HasFlag(MouseButtons.Left))
                return 255;
            if (mappedMouse == 1 && Control.MouseButtons.HasFlag(MouseButtons.Right))
                return 255;
            return 0;
        }

        // Helper to update button state only if changed
        private void UpdateButtonState(Xbox360Button button, bool currentState, int stateIndex)
        {
            if (currentState != _previousButtonStates[stateIndex])
            {
                controller?.SetButtonState(button, currentState);
                _previousButtonStates[stateIndex] = currentState;
            }
        }
        private async void Perform360Macro()
        {
            if (controller == null) return;

            int steps = 36; // Number of incremental steps for a smooth rotation
            int delayMs = 15; // delay between each step (adjust for speed)
            float maxValue = 32767f; // max thumbstick value

            // Rotate right: from 0 to max
            for (int i = 0; i <= steps; i++)
            {
                float value = (float)i / steps * maxValue;
                controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)value);
                await Task.Delay(delayMs);
            }

            // Optional: hold at max for a moment for a more natural turn
            await Task.Delay(100);

            // Return to center
            controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F6)
                ToggleVirtualController();
            else if (e.KeyCode == Keys.Home)
                TogglePause();

            // Check if 360 macro is active and '3' is pressed
            if (is360MacroActive && e.KeyCode == Keys.D3)
            {
                Perform360Macro();
                e.Handled = true; // Prevent default behavior if needed
            }
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
        // Add these with your other constants
        private enum ResponseCurveType { Linear, Exponential, Logarithmic, Custom }
        private ResponseCurveType currentCurveType = ResponseCurveType.Logarithmic; // instead of Exponential

        // Curve parameters
        private const float expCurveStrength = 2.0f; // Higher = more aggressive curve
        private const float logCurveStrength = 0.3f; // Smaller values increase sensitivity for small inputs
        private const float customCurveMidpoint = 0.5f; // Adjust as needed
        private const float customCurveSteepness = 2.0f; // Adjust as needed

        // Adaptive smoothing parameters
        private const float minSmoothing = 0.05f; // For slow movements
        private const float maxSmoothing = 0.3f; // For fast movements
        private const float smoothingTransitionSpeed = 0.1f; // How quickly smoothing adjusts
        private float ApplyResponseCurve(float input, ResponseCurveType curveType)
        {
            // Normalize input to 0-1 range
            float normalized = Math.Abs(input) / maxAnalog;
            float result = 0f;

            switch (curveType)
            {
                case ResponseCurveType.Linear:
                    result = normalized;
                    break;

                case ResponseCurveType.Exponential:
                    result = (float)Math.Pow(normalized, expCurveStrength);
                    break;

                case ResponseCurveType.Logarithmic:
                    result = (float)Math.Log(normalized * (Math.E - 1) + 1) * logCurveStrength;
                    break;

                case ResponseCurveType.Custom:
                    // Custom sigmoid-like curve
                    result = (float)(1 / (1 + Math.Exp(-customCurveSteepness * (normalized - customCurveMidpoint))));
                    break;
            }

            // Scale back to original range and preserve sign
            return Math.Sign(input) * result * maxAnalog;
        }

        private float CalculateAdaptiveSmoothing(float deltaX, float deltaY)
        {
            // Calculate movement magnitude
            float magnitude = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Map magnitude to smoothing factor (non-linear mapping)
            float smoothing = minSmoothing + (maxSmoothing - minSmoothing) *
                             (1 - (float)Math.Exp(-magnitude / 50f)); // Adjust divisor as needed

            return Clamp(smoothing, minSmoothing, maxSmoothing);
        }

        private void panel25_Paint(object sender, PaintEventArgs e)
        {

        }

        private void AdvancedSettingsPanel_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button9_Click(object sender, EventArgs e)
        {
            AdvancedSettingsPanel.Show();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            AdvancedSettingsPanel.Hide();
        }

        private void label10_Click(object sender, EventArgs e)
        {

        }
        //Smoothing modes
        private enum SmoothingMode { Linear, Exponential }
        private SmoothingMode currentSmoothingMode = SmoothingMode.Linear;
        private float LinearInterpolation(float current, float target, float factor)
        {
            return current + (target - current) * factor;
        }

        private float ExponentialSmoothing(float current, float target, float factor)
        {
            // Exponential smoothing formula: Sₜ = αYₜ + (1-α)Sₜ₋₁
            return factor * target + (1 - factor) * current;
        }
        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox11.Checked)
            {
                checkBox10.Checked = false;
                currentSmoothingMode = SmoothingMode.Linear;
            }
        }

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox10.Checked)
            {
                checkBox11.Checked = false;
                currentSmoothingMode = SmoothingMode.Exponential;
            }
        }
        //Sensitivity controller
        private const float baseMouseSensitivityX = 200f;
        private const float baseMouseSensitivityY = 200f;
        private float currentMouseSensitivityX = baseMouseSensitivityX;
        private float currentMouseSensitivityY = baseMouseSensitivityY;
        private void UpdateSensitivityLabel()
        {
            sensitivityLabel.Text = $"Sensitivity: {sensitivityTrackBar.Value}";
        }
        private void SensitivityTrackBar_Scroll(object sender, EventArgs e)
        {
            // Map trackbar value (1-10) to sensitivity multiplier (0.5x-2.0x)
            float sensitivityMultiplier = 0.5f + (sensitivityTrackBar.Value - 1) * 0.1667f;

            currentMouseSensitivityX = baseMouseSensitivityX * sensitivityMultiplier;
            currentMouseSensitivityY = baseMouseSensitivityY * sensitivityMultiplier;

            // Shows current sensitivity
            sensitivityLabel.Text = $"Sensitivity: {sensitivityTrackBar.Value}";

            // Saves
            UpdateSensitivityFromTrackbar();
        }

        private void sensitivityTrackBar_Scroll(object sender, EventArgs e)
        {

        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            is360MacroActive = checkBox7.Checked;
            if (is360MacroActive && show360MacroNotification)
            {
                ShowCustomNotification("360 Macro", "Activated");
            }
            else if (!is360MacroActive && show360MacroNotification)
            {
                ShowCustomNotification("360 Macro", "Deactivated");
            }
        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
{
    show360MacroNotification = checkBox9.Checked;
    if (!show360MacroNotification && checkBox7.Checked)
    {
        // Notify that 360 Macro notifications are now disabled
        ShowCustomNotification("360 Macro", "Notifications Disabled");
    }
}
        private void pictureBox1_Click(object sender, EventArgs e) { }
    }
}
