using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Loading animation variant styles.
    /// </summary>
    public enum DaisyLoadingVariant
    {
        /// <summary>Spinner animation (default) - rotating arc</summary>
        Spinner,
        /// <summary>Dots animation - three bouncing dots</summary>
        Dots,
        /// <summary>Ring animation - rotating ring</summary>
        Ring,
        /// <summary>Ball animation - bouncing ball</summary>
        Ball,
        /// <summary>Bars animation - three animated bars</summary>
        Bars,
        /// <summary>Infinity animation - infinity symbol path</summary>
        Infinity,
        /// <summary>Orbit animation - dots orbiting around a square (terminal-style)</summary>
        Orbit,
        /// <summary>Snake animation - centipede-like segments moving back and forth</summary>
        Snake,
        /// <summary>Pulse animation - breathing/pulsing effect</summary>
        Pulse,
        /// <summary>Wave animation - multiple elements creating a wave</summary>
        Wave,
        /// <summary>Bounce animation - bouncing squares</summary>
        Bounce,
        /// <summary>Matrix animation - colon-dotted pattern with wave moving left to right</summary>
        Matrix,
        /// <summary>MatrixInward animation - both groups fade from inner to outer dots</summary>
        MatrixInward,
        /// <summary>MatrixOutward animation - both groups fade from outer to inner dots</summary>
        MatrixOutward,
        /// <summary>MatrixVertical animation - wave moves top to bottom across all dots</summary>
        MatrixVertical,
        /// <summary>MatrixRain animation - digital rain of dots falling down</summary>
        MatrixRain,
        /// <summary>Hourglass animation - classic hourglass with flowing sand</summary>
        Hourglass,
        /// <summary>SignalSweep animation - oscilloscope bar scanning left to right</summary>
        SignalSweep,
        /// <summary>BitFlip animation - dots flip like binary on/off states</summary>
        BitFlip,
        /// <summary>PacketBurst animation - dot shoots to edges and returns to center</summary>
        PacketBurst,
        /// <summary>CometTrail animation - bright dot with fading tail in a loop</summary>
        CometTrail,
        /// <summary>Heartbeat animation - EKG-style pulse line</summary>
        Heartbeat,
        /// <summary>TunnelZoom animation - concentric rings expanding outward</summary>
        TunnelZoom,
        /// <summary>GlitchReveal animation - random columns flash like terminal glitch</summary>
        GlitchReveal,
        /// <summary>RippleMatrix animation - brightness ripples outward from center</summary>
        RippleMatrix,
        /// <summary>CursorBlink animation - classic CLI cursor that moves and blinks</summary>
        CursorBlink,
        /// <summary>CountdownSpinner animation - 12 dots toggle like clock ticking</summary>
        CountdownSpinner,

        // ==================== BUSINESS VARIANTS ====================
        /// <summary>DocumentFlipOn animation - document page flipping animation (opening)</summary>
        DocumentFlipOn,
        /// <summary>DocumentFlipOff animation - document page flipping animation (closing)</summary>
        DocumentFlipOff,
        /// <summary>MailSend animation - envelope sending animation</summary>
        MailSend,
        /// <summary>CloudUpload animation - cloud with uploading arrow</summary>
        CloudUpload,
        /// <summary>CloudDownload animation - cloud with downloading arrow</summary>
        CloudDownload,
        /// <summary>DocumentStamp animation - document being stamped with OK</summary>
        DocumentStamp,
        /// <summary>DocumentReject animation - document being stamped with X (rejected)</summary>
        DocumentReject,
        /// <summary>ChartPulse animation - bar chart with pulsing bars</summary>
        ChartPulse,
        /// <summary>CalendarTick animation - calendar with checkmark appearing</summary>
        CalendarTick,
        /// <summary>ApprovalFlow animation - workflow with approval checkmarks</summary>
        ApprovalFlow,
        /// <summary>BriefcaseSpin animation - spinning briefcase</summary>
        BriefcaseSpin,
        /// <summary>BatteryCharging animation - segmented battery filling from empty to full</summary>
        BatteryCharging,
        /// <summary>BatteryEmptying animation - segmented battery draining from full to empty</summary>
        BatteryEmptying,
        /// <summary>TrafficLightUp animation - vertical traffic light cycling upward</summary>
        TrafficLightUp,
        /// <summary>TrafficLightRight animation - traffic light cycling toward the right</summary>
        TrafficLightRight,
        /// <summary>TrafficLightDown animation - vertical traffic light cycling downward</summary>
        TrafficLightDown,
        /// <summary>TrafficLightLeft animation - traffic light cycling toward the left</summary>
        TrafficLightLeft,
        /// <summary>PrinterOutput animation - printer feeding pages out</summary>
        PrinterOutput,
        /// <summary>PaperShredder animation - document being shredded into strips</summary>
        PaperShredder,
        /// <summary>SignaturePen animation - pen signing a document</summary>
        SignaturePen,
        /// <summary>DocumentScan animation - scan beam moving over a document</summary>
        DocumentScan,
        /// <summary>FolderSync animation - folders syncing with a moving document</summary>
        FolderSync,
        /// <summary>MailReceive animation - incoming envelope opening with a document</summary>
        MailReceive,
        /// <summary>PhoneRing animation - phone handset ringing with wave arcs</summary>
        PhoneRing,
        /// <summary>CoinStack animation - coins falling onto a growing stack</summary>
        CoinStack,
        /// <summary>InvoicePaid animation - invoice being stamped as paid</summary>
        InvoicePaid,
        /// <summary>PiggyBank animation - coin dropping into a piggy bank</summary>
        PiggyBank,
        /// <summary>PieChartFill animation - pie chart segments appearing in sequence</summary>
        PieChartFill,
        /// <summary>TrendLine animation - analytical trend line drawing upward</summary>
        TrendLine,
        /// <summary>ClockSpin animation - clock hands spinning while waiting</summary>
        ClockSpin,
        /// <summary>CoffeeCup animation - coffee cup with rising steam</summary>
        CoffeeCup,

        // ==================== WIN95 RETRO VARIANTS ====================
        /// <summary>Win95FileCopy animation - classic Windows 95 file copy with flying papers</summary>
        Win95FileCopy,
        /// <summary>Win95Delete animation - classic Windows 95 file delete with papers going to trash</summary>
        Win95Delete,
        /// <summary>Win95Search animation - classic Windows 95 magnifying glass search</summary>
        Win95Search,
        /// <summary>Win95EmptyRecycle animation - classic Windows 95 recycle bin emptying</summary>
        Win95EmptyRecycle,
        /// <summary>Win95Defrag animation - classic block defragmentation grid</summary>
        Win95Defrag,
        /// <summary>Win95Download animation - paper downloading from globe to folder</summary>
        Win95Download,
        /// <summary>Win95Install animation - floppy disk sliding into a drive</summary>
        Win95Install,
        /// <summary>Win95ScanDisk animation - scan boxes checking across a disk row</summary>
        Win95ScanDisk,
        /// <summary>Win95Hourglass animation - chunky retro hourglass cursor</summary>
        Win95Hourglass,
        /// <summary>Win95DialUp animation - two computers exchanging dial-up signals</summary>
        Win95DialUp,
        /// <summary>Win95Solitaire animation - cards cascading after a win</summary>
        Win95Solitaire,
        /// <summary>Win95PrintQueue animation - retro printer output queue</summary>
        Win95PrintQueue,
        /// <summary>Win95FindComputer animation - magnifying glass searching a PC</summary>
        Win95FindComputer,
        /// <summary>Win95Startup animation - four startup squares lighting in sequence</summary>
        Win95Startup,
        /// <summary>Win95StartupColor animation - four Windows-colored squares lighting in sequence</summary>
        Win95StartupColor
    }

    /// <summary>
    /// A Loading control styled after DaisyUI's Loading component.
    /// Shows an animation to indicate that something is loading.
    /// Includes accessibility support for screen readers via the AccessibleText attached property.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyLoading : TemplatedControl, IScalableControl
    {
        private const string DefaultAccessibleTextKey = "Accessibility_Loading";
        private const string DefaultAccessibleTextFallback = "Loading";
        private const double BaseTextFontSize = 14.0;

        protected override Type StyleKeyOverride => typeof(DaisyLoading);

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = Services.FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        static DaisyLoading()
        {
            // Use fallback for static initialization; runtime will use localized string
            DaisyAccessibility.SetupAccessibility<DaisyLoading>(DefaultAccessibleTextFallback);
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyLoadingVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyLoading, DaisyLoadingVariant>(nameof(Variant), DaisyLoadingVariant.Spinner);

        /// <summary>
        /// Gets or sets the loading animation variant (Spinner, Dots, Ring, Ball, Bars, Infinity).
        /// </summary>
        public DaisyLoadingVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyLoading, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the loading indicator (ExtraSmall, Small, Medium, Large, ExtraLarge).
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Color"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyColor> ColorProperty =
            AvaloniaProperty.Register<DaisyLoading, DaisyColor>(nameof(Color), DaisyColor.Default);

        /// <summary>
        /// Gets or sets the color variant (Default, Primary, Secondary, Accent, etc.).
        /// </summary>
        public DaisyColor Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the accessible text announced by screen readers.
        /// Default is "Loading". Set to a more specific message like "Loading data" or "Please wait".
        /// </summary>
        public string? AccessibleText
        {
            get => DaisyAccessibility.GetAccessibleText(this);
            set => DaisyAccessibility.SetAccessibleText(this, value);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DaisyLoadingAutomationPeer(this);
        }
    }

    /// <summary>
    /// AutomationPeer for DaisyLoading that exposes it as a ProgressBar to assistive technologies.
    /// </summary>
    internal class DaisyLoadingAutomationPeer : ControlAutomationPeer
    {
        private const string DefaultAccessibleText = "Loading";

        public DaisyLoadingAutomationPeer(DaisyLoading owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.ProgressBar;
        }

        protected override string GetClassNameCore()
        {
            return "DaisyLoading";
        }

        protected override string? GetNameCore()
        {
            var loading = (DaisyLoading)Owner;
            var localizedDefault = FloweryLocalization.GetStringInternal("Accessibility_Loading");
            return DaisyAccessibility.GetEffectiveAccessibleText(loading, localizedDefault);
        }

        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
    }
}
