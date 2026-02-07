using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Helpers;
using Flowery.Services;
using Flowery.Theming;

namespace Flowery.NET.Gallery.Examples
{
    public sealed partial class ProductThemesExamples : UserControl, IScrollableExample
    {
        private Dictionary<string, Visual>? _sectionTargetsById;

        private string _currentIndustry = "SaaS";
        private bool _isFormSelectorSyncing;

        private static readonly (string Industry, string DisplayName, string ThemeName)[] IndustryForms =
        [
            ("Insurance", "Insurance", "Insurance"),
            ("Healthcare", "Healthcare", "HealthcareApp"),
            ("RealEstate", "Real Estate", "RealEstate"),
            ("Banking", "Banking", "BankingFinance"),
            ("Travel", "Travel", "TravelTourism"),
            ("Restaurant", "Restaurant", "RestaurantFood"),
            ("Legal", "Legal", "LegalServices"),
            ("Education", "Education", "EducationalApp"),
            ("JobBoard", "Job Board", "JobBoard"),
            ("Hotel", "Hotel", "HotelHospitality"),
            ("Fitness", "Fitness", "FitnessGym"),
            ("Ecommerce", "E-commerce", "Ecommerce"),
            ("Gaming", "Gaming", "Gaming"),
            ("Creative", "Creative", "CreativeAgency"),
            ("AI", "AI", "AIChatbot"),
            ("Security", "Security", "Cybersecurity"),
            ("Space", "Space Tech", "SpaceTech"),
            ("Dating", "Dating", "Dating"),
            ("Pet", "Pet", "PetTech"),
            ("Sustainability", "Sustainability", "SustainabilityESG"),
            ("NonProfit", "Non-Profit", "NonProfit"),
            ("Sports", "Sports", "SportsTeam"),
            ("Arts", "Arts & Entertainment", "MuseumGallery"),
            ("Design", "Design", "DesignSystem"),
            ("ERP", "ERP / Services", "CleaningService"),
            ("Media", "Media", "NewsMedia"),
            ("SaaS", "SaaS", "SaaS")
        ];

        private static readonly Dictionary<string, string> IndustryToTheme = IndustryForms
            .ToDictionary(entry => entry.Industry, entry => entry.ThemeName, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> IndustryToDisplayName = IndustryForms
            .ToDictionary(entry => entry.Industry, entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> DisplayNameToIndustry = IndustryForms
            .ToDictionary(entry => entry.DisplayName, entry => entry.Industry, StringComparer.OrdinalIgnoreCase);

        // Maps product theme names to their industry category for form selection
        private static readonly Dictionary<string, string> ThemeToIndustry = new(StringComparer.OrdinalIgnoreCase)
        {
            // Insurance
            { "Insurance", "Insurance" },
            // Healthcare
            { "HealthcareApp", "Healthcare" }, { "MedicalClinic", "Healthcare" }, { "MentalHealth", "Healthcare" },
            { "Pharmacy", "Healthcare" }, { "DentalPractice", "Healthcare" }, { "VeterinaryClinic", "Healthcare" },
            { "Biotech", "Healthcare" },
            // Real Estate
            { "RealEstate", "RealEstate" }, { "ArchitectureInterior", "RealEstate" }, { "Construction", "RealEstate" },
            // Banking/Finance
            { "BankingFinance", "Banking" }, { "FinancialDashboard", "Banking" }, { "FintechCrypto", "Banking" },
            // Travel
            { "TravelTourism", "Travel" }, { "Airline", "Travel" },
            // Restaurant/Food
            { "RestaurantFood", "Restaurant" }, { "BakeryCafe", "Restaurant" }, { "CoffeeShop", "Restaurant" },
            { "BreweryWinery", "Restaurant" },
            // Legal
            { "LegalServices", "Legal" }, { "Government", "Legal" }, { "Consulting", "Legal" },
            // Education
            { "EducationalApp", "Education" }, { "OnlineCourse", "Education" }, { "LanguageLearning", "Education" },
            { "CodingBootcamp", "Education" }, { "MicroCredentials", "Education" },
            // Job/Recruitment
            { "JobBoard", "JobBoard" }, { "Freelancer", "JobBoard" }, { "Coworking", "JobBoard" },
            // Hotel/Hospitality
            { "HotelHospitality", "Hotel" }, { "WeddingEvent", "Hotel" }, { "EventManagement", "Hotel" },
            // Fitness
            { "FitnessGym", "Fitness" }, { "BeautySpaWellness", "Fitness" },
            // E-commerce
            { "Ecommerce", "Ecommerce" }, { "EcommerceLuxury", "Ecommerce" }, { "SubscriptionBox", "Ecommerce" },
            { "MarketplaceP2P", "Ecommerce" }, { "DigitalProducts", "Ecommerce" },
            // Gaming
            { "Gaming", "Gaming" },
            // Creative/Agency/Portfolio
            { "CreativeAgency", "Creative" }, { "Portfolio", "Creative" }, { "MarketingAgency", "Creative" },
            { "Photography", "Creative" }, { "GenerativeAIArt", "Creative" },
            // Design
            { "DesignSystem", "Design" },
            // AI/Chatbot
            { "AIChatbot", "AI" },
            // Security
            { "Cybersecurity", "Security" },
            // Space/High Tech
            { "SpaceTech", "Space" }, { "QuantumComputing", "Space" }, { "AutonomousSystems", "Space" },
            // Dating
            { "Dating", "Dating" },
            // Pet Tech
            { "PetTech", "Pet" },
            // Sustainability/Environment
            { "SustainabilityESG", "Sustainability" }, { "ClimateTech", "Sustainability" },
            // Non-Profit
            { "NonProfit", "NonProfit" },
            // Sports
            { "SportsTeam", "Sports" },
            // Arts & Entertainment
            { "MuseumGallery", "Arts" }, { "TheaterCinema", "Arts" }, { "MusicStreaming", "Arts" }, { "VideoStreamingOTT", "Arts" }, { "Podcast", "Arts" },
            // ERP / Service Business (customer management forms)
            { "CleaningService", "ERP" }, { "HomeServices", "ERP" }, { "Childcare", "ERP" },
            { "SeniorCare", "ERP" }, { "LogisticsDelivery", "ERP" }, { "AgricultureFarm", "ERP" },
            { "ChurchReligious", "ERP" }, { "ServiceLanding", "ERP" }, { "B2BService", "ERP" },
            { "HyperlocalServices", "ERP" },
            // Media/Content
            { "NewsMedia", "Media" }, { "MagazineBlog", "Media" }, { "Newsletter", "Media" },
            { "ConferenceWebinar", "Media" }, { "MembershipCommunity", "Media" },
            // Tech/SaaS (default)
            { "SaaS", "SaaS" }, { "MicroSaaS", "SaaS" }, { "AnalyticsDashboard", "SaaS" },
            { "ProductivityTool", "SaaS" }, { "DeveloperTool", "SaaS" },
            { "RemoteWork", "SaaS" }, { "SmartHomeIoT", "SaaS" },
            { "SpatialVisionOS", "SaaS" }, { "KnowledgeBase", "SaaS" }
        };

        public ProductThemesExamples()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            FlowerySizeManager.SizeChanged += OnGlobalSizeChanged;
            ApplyIntroSizes(FlowerySizeManager.CurrentSize);

            // Subscribe to theme changes (fires AFTER theme is applied)
            DaisyThemeManager.ThemeChanged += OnThemeChanged;

            InitializeFormSelector();

            // Build initial form and apply initial styling
            ApplyFormCardTheme();
            _currentIndustry = GetIndustryForTheme("SaaS");
            BuildFormForIndustry(_currentIndustry);
            SyncFormSelectorToIndustry(_currentIndustry);
            SyncProductThemeDropdownToTheme(GetThemeForIndustry(_currentIndustry));
        }

        private void ApplyFormCardTheme()
        {
            // Manually update card colors since ThemeResource doesn't auto-refresh on MergedDictionaries change
            var bg = DaisyResourceLookup.GetBrush("DaisyBase200Brush") ?? Brushes.Transparent;
            var border = DaisyResourceLookup.GetBrush("DaisyBase300Brush") ?? Brushes.Transparent;

            IntroCard.Background = bg;
            IntroCard.BorderBrush = border;

            // Update FormWrapper border (background stays transparent)
            FormWrapper.BorderBrush = border;
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            FlowerySizeManager.SizeChanged -= OnGlobalSizeChanged;
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, string themeName)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnThemeChanged(sender, themeName));
                return;
            }

            // Update form card colors for new theme (called AFTER theme is applied)
            ApplyFormCardTheme();

            // Always rebuild form when theme changes (to pick up new colors)
            // even if industry category stays the same
            var industry = GetIndustryForTheme(themeName);
            _currentIndustry = industry;
            BuildFormForIndustry(industry);
            SyncFormSelectorToIndustry(industry);
            SyncProductThemeDropdownToTheme(themeName);

            // Auto-scroll to theme selector after form is rendered
            if (AutoScrollCheckBox.IsChecked == true)
            {
                Dispatcher.UIThread.Post(ScrollThemeSelectorIntoView, DispatcherPriority.Background);
            }
        }

        private void ScrollThemeSelectorIntoView()
        {
            var transform = FormSelectorRow.TransformToVisual(MainScrollViewer);
            if (transform.HasValue)
            {
                var position = transform.Value.Transform(new Point(0, 0));
                MainScrollViewer.Offset = new Vector(0, Math.Max(0, position.Y + MainScrollViewer.Offset.Y - 8));
            }
        }

        private static string GetIndustryForTheme(string themeName)
        {
            return ThemeToIndustry.TryGetValue(themeName, out var industry) ? industry : "SaaS";
        }

        private static string GetThemeForIndustry(string industry)
        {
            return IndustryToTheme.TryGetValue(industry, out var themeName) ? themeName : "SaaS";
        }

        private void InitializeFormSelector()
        {
            FormSelector.Items.Clear();
            foreach (var (Industry, DisplayName, ThemeName) in IndustryForms
                         .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                FormSelector.Items.Add(DisplayName);
            }

            FormSelector.SelectionChanged += OnFormSelectorChanged;
            SyncFormSelectorToIndustry(_currentIndustry);
        }

        private void OnFormSelectorChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isFormSelectorSyncing)
            {
                return;
            }

            if (FormSelector.SelectedItem is not string displayName)
            {
                return;
            }

            if (!DisplayNameToIndustry.TryGetValue(displayName, out var industry))
            {
                return;
            }

            _currentIndustry = industry;
            BuildFormForIndustry(industry);
            ApplyProductTheme(GetThemeForIndustry(industry));
        }

        private void SyncFormSelectorToIndustry(string industry)
        {
            if (!IndustryToDisplayName.TryGetValue(industry, out var displayName))
            {
                return;
            }

            if (FormSelector.SelectedItem is string current && string.Equals(current, displayName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _isFormSelectorSyncing = true;
            try
            {
                FormSelector.SelectedItem = displayName;
            }
            finally
            {
                _isFormSelectorSyncing = false;
            }
        }

        private static void ApplyProductTheme(string themeName)
        {
            var palette = ProductPalettes.Get(themeName);
            if (palette == null)
            {
                return;
            }

            var isDark = FloweryColorHelpers.IsDark(palette.Base100);
            DaisyThemeManager.RegisterTheme(
                new DaisyThemeInfo(themeName, isDark),
                () => DaisyPaletteFactory.Create(palette));
            DaisyThemeManager.ApplyTheme(themeName);
        }

        private void SyncProductThemeDropdownToTheme(string themeName)
        {
            if (ProductThemeDropdown.ItemsSource is not IEnumerable<ProductThemePreviewInfo> themes)
            {
                return;
            }

            var match = themes.FirstOrDefault(theme => string.Equals(theme.Name, themeName, StringComparison.OrdinalIgnoreCase));
            if (match == null || ReferenceEquals(ProductThemeDropdown.SelectedItem, match))
            {
                return;
            }

            var previousApply = ProductThemeDropdown.ApplyOnSelection;
            try
            {
                ProductThemeDropdown.ApplyOnSelection = false;
                ProductThemeDropdown.SelectedItem = match;
                ProductThemeDropdown.SelectedTheme = match.Name;
            }
            finally
            {
                ProductThemeDropdown.ApplyOnSelection = previousApply;
            }
        }

        private void OnGlobalSizeChanged(object? sender, DaisySize size)
        {
            ApplyIntroSizes(size);

            // Also update all controls in the current form
            if (FormContainer.Children.Count > 0 && FormContainer.Children[0] is Control form)
            {
                ApplySizeToElement(form, size);
            }
        }

        private void ApplyIntroSizes(DaisySize size)
        {
            // Header tier: 14/16/20/24/28
            double headerFontSize = size switch
            {
                DaisySize.ExtraSmall => 14,
                DaisySize.Small => 16,
                DaisySize.Medium => 20,
                DaisySize.Large => 24,
                DaisySize.ExtraLarge => 28,
                _ => 16
            };

            // Primary tier (body text): 10/12/14/18/20
            double primaryFontSize = size switch
            {
                DaisySize.ExtraSmall => 10,
                DaisySize.Small => 12,
                DaisySize.Medium => 14,
                DaisySize.Large => 18,
                DaisySize.ExtraLarge => 20,
                _ => 12
            };

            // Secondary tier (labels): 9/10/12/14/16
            double secondaryFontSize = size switch
            {
                DaisySize.ExtraSmall => 9,
                DaisySize.Small => 10,
                DaisySize.Medium => 12,
                DaisySize.Large => 14,
                DaisySize.ExtraLarge => 16,
                _ => 10
            };

            IntroTitle.FontSize = headerFontSize;
            IntroBody1.FontSize = primaryFontSize;
            IntroBody2.FontSize = primaryFontSize;
            IntroCreditsTitle.FontSize = secondaryFontSize;
            IntroCreditsBody.FontSize = secondaryFontSize;
            FormDescription.FontSize = primaryFontSize;
        }

        private void BuildFormForIndustry(string industry)
        {
            FormContainer.Children.Clear();

            var form = industry switch
            {
                "Insurance" => BuildInsuranceForm(),
                "Healthcare" => BuildHealthcareForm(),
                "RealEstate" => BuildRealEstateForm(),
                "Banking" => BuildBankingForm(),
                "Travel" => BuildTravelForm(),
                "Restaurant" => BuildRestaurantForm(),
                "Legal" => BuildLegalForm(),
                "Education" => BuildEducationForm(),
                "JobBoard" => BuildJobBoardForm(),
                "Hotel" => BuildHotelForm(),
                "Fitness" => BuildFitnessForm(),
                "Ecommerce" => BuildEcommerceForm(),
                "Gaming" => BuildGamingForm(),
                "Creative" => BuildCreativeForm(),
                "AI" => BuildAIForm(),
                "Security" => BuildSecurityForm(),
                "Space" => BuildSpaceTechForm(),
                "Dating" => BuildDatingForm(),
                "Pet" => BuildPetForm(),
                "Sustainability" => BuildSustainabilityForm(),
                "NonProfit" => BuildNonProfitForm(),
                "Sports" => BuildSportsForm(),
                "Arts" => BuildArtsForm(),
                "Design" => BuildDesignForm(),
                "ERP" => BuildERPForm(),
                "Media" => BuildMediaForm(),
                _ => BuildSaaSForm()
            };

            EnsureWrappingOptionRows(form);
            FormContainer.Children.Add(form);

            // Apply size immediately
            ApplySizeToElement(form, FlowerySizeManager.CurrentSize);
        }

        private static void EnsureWrappingOptionRows(Control root)
        {
            if (root is Panel panel)
            {
                for (var i = 0; i < panel.Children.Count; i++)
                {
                    if (panel.Children[i] is StackPanel stack && ShouldWrapOptionRow(stack))
                    {
                        var wrap = new WrapPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = stack.Margin,
                            HorizontalAlignment = stack.HorizontalAlignment,
                            VerticalAlignment = stack.VerticalAlignment
                        };

                        while (stack.Children.Count > 0)
                        {
                            var wrappedChild = stack.Children[0];
                            stack.Children.RemoveAt(0);

                            if (wrappedChild is Control wrappedControl)
                            {
                                var current = wrappedControl.Margin;
                                wrappedControl.Margin = new Thickness(
                                    current.Left,
                                    current.Top,
                                    Math.Max(current.Right, stack.Spacing),
                                    Math.Max(current.Bottom, 8));
                            }

                            wrap.Children.Add(wrappedChild);
                        }

                        panel.Children.RemoveAt(i);
                        panel.Children.Insert(i, wrap);
                    }

                    if (panel.Children[i] is Control childControl)
                    {
                        EnsureWrappingOptionRows(childControl);
                    }
                }

                return;
            }

            if (root is Border { Child: Control child })
            {
                EnsureWrappingOptionRows(child);
                return;
            }

            if (root is ContentControl { Content: Control content })
            {
                EnsureWrappingOptionRows(content);
            }
        }

        private static bool ShouldWrapOptionRow(StackPanel stack)
        {
            if (stack.Orientation != Orientation.Horizontal || stack.Children.Count < 2)
                return false;

            var hasOptionControl = false;

            foreach (var child in stack.Children)
            {
                if (child is DaisyCheckBox or DaisyRadio or DaisyToggle)
                {
                    hasOptionControl = true;
                    continue;
                }

                if (child is TextBlock)
                    continue;

                return false;
            }

            return hasOptionControl;
        }

        /// <summary>
        /// Recursively applies the given size to all Daisy controls in the element tree.
        /// </summary>
        private static void ApplySizeToElement(Control element, DaisySize size)
        {
            // Apply size to known Daisy control types (derived types must come before base types)
            switch (element)
            {
                case DaisyTextArea textarea: textarea.Size = size; break; // Must be before DaisyInput (inherits from it)
                case DaisyInput input: input.Size = size; break;
                case DaisySelect select: select.Size = size; break;
                case DaisyCheckBox checkbox: checkbox.Size = size; break;
                case DaisyRadio radio: radio.Size = size; break;
                case DaisyButton button: button.Size = size; break;
                case DaisyBadge badge: badge.Size = size; break;
                case DaisyToggle toggle: toggle.Size = size; break;
                case DaisyFileInput fileInput: fileInput.Size = size; break;
                case TextBlock textBlock when textBlock.Tag is ResponsiveFontTier tier:
                    textBlock.FontSize = FlowerySizeManager.GetFontSizeForTier(tier, size);
                    break;
            }

            // Recurse into children
            if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                    ApplySizeToElement(child, size);
            }
            else if (element is Border border && border.Child != null)
            {
                ApplySizeToElement(border.Child, size);
            }
            else if (element is ContentControl cc && cc.Content is Control content)
            {
                ApplySizeToElement(content, size);
            }
        }

        private static TextBlock CreateFormTitle(string title) => new()
        {
            Text = title,
            FontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Header, FlowerySizeManager.CurrentSize),
            Tag = ResponsiveFontTier.Header,
            FontWeight = FontWeight.SemiBold,
            Foreground = GetBrush("DaisyPrimaryBrush")
        };

        private static TextBlock CreateSectionLabel(string text) => new()
        {
            Text = text,
            FontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.SectionHeader, FlowerySizeManager.CurrentSize),
            Tag = ResponsiveFontTier.SectionHeader,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.8,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = GetBrush("DaisyBaseContentBrush")
        };

        private static IBrush? GetBrush(string key)
        {
            if (Application.Current?.TryGetResource(key, null, out var value) == true && value is IBrush brush)
                return brush;
            return null;
        }

        /// <summary>
        /// Preserves the Uno form builder API shape while Flowery.NET has no neumorphic attached behavior.
        /// </summary>
        private static T WithNeumorphic<T>(T control) where T : Control
        {
            return control;
        }

        private static StackPanel CreateLabeledSelect(string label, DaisySelect select)
        {
            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Secondary, FlowerySizeManager.CurrentSize),
                Tag = ResponsiveFontTier.Secondary,
                Opacity = 0.9,
                Foreground = GetBrush("DaisyBaseContentBrush")
            });
            panel.Children.Add(select);
            return panel;
        }

        private static StackPanel CreateLabeledFileInput(string label, DaisyFileInput fileInput)
        {
            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Secondary, FlowerySizeManager.CurrentSize),
                Tag = ResponsiveFontTier.Secondary,
                Opacity = 0.9,
                Foreground = GetBrush("DaisyBaseContentBrush")
            });
            panel.Children.Add(fileInput);
            return panel;
        }

        #region Insurance Form
        private static StackPanel BuildInsuranceForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🛡️ Insurance Claim Form"));

            // Policy Information
            panel.Children.Add(CreateSectionLabel("Policy Information"));
            var policyGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16, RowSpacing = 12 };
            policyGrid.RowDefinitions.Add(new RowDefinition());
            policyGrid.RowDefinitions.Add(new RowDefinition());

            var policyNumber = WithNeumorphic(new DaisyInput { Label = "Policy Number", Watermark = "POL-2024-XXXXXX", IsRequired = true });
            Grid.SetColumn(policyNumber, 0); Grid.SetRow(policyNumber, 0);
            policyGrid.Children.Add(policyNumber);

            var claimType = WithNeumorphic(new DaisySelect());
            claimType.Items.Add("Auto Collision"); claimType.Items.Add("Property Damage"); claimType.Items.Add("Theft"); claimType.Items.Add("Liability"); claimType.Items.Add("Medical");
            claimType.SelectedIndex = 0;
            var claimTypeLabeled = CreateLabeledSelect("Claim Type", claimType);
            Grid.SetColumn(claimTypeLabeled, 1); Grid.SetRow(claimTypeLabeled, 0);
            policyGrid.Children.Add(claimTypeLabeled);

            var incidentDate = WithNeumorphic(new DaisyInput { Label = "Date of Incident", Watermark = "MM/DD/YYYY", IsRequired = true });
            Grid.SetColumn(incidentDate, 0); Grid.SetRow(incidentDate, 1);
            policyGrid.Children.Add(incidentDate);

            var estimatedLoss = WithNeumorphic(new DaisyInput { Label = "Estimated Loss ($)", Watermark = "0.00" });
            Grid.SetColumn(estimatedLoss, 1); Grid.SetRow(estimatedLoss, 1);
            policyGrid.Children.Add(estimatedLoss);

            panel.Children.Add(policyGrid);

            // Incident Details
            panel.Children.Add(CreateSectionLabel("Incident Details"));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Location of Incident", Watermark = "Address where incident occurred" }));
            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Description of Incident", Watermark = "Please describe what happened in detail...", MinHeight = 100 }));

            // Witnesses
            panel.Children.Add(CreateSectionLabel("Witnesses (if any)"));
            var witnessGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var witnessName = WithNeumorphic(new DaisyInput { Label = "Witness Name", Watermark = "Full name" });
            Grid.SetColumn(witnessName, 0);
            witnessGrid.Children.Add(witnessName);
            var witnessPhone = WithNeumorphic(new DaisyInput { Label = "Witness Phone", Watermark = "(555) 123-4567" });
            Grid.SetColumn(witnessPhone, 1);
            witnessGrid.Children.Add(witnessPhone);
            panel.Children.Add(witnessGrid);

            // Declarations
            panel.Children.Add(new DaisyDivider { Content = "Declarations" });
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I confirm all information provided is accurate and complete", Variant = DaisyCheckBoxVariant.Primary }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I understand fraudulent claims may result in policy cancellation", Variant = DaisyCheckBoxVariant.Warning }));

            // Submit
            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Submit Claim", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save Draft", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Healthcare Form
        private static StackPanel BuildHealthcareForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🏥 Patient Registration Form"));

            // Personal Information
            panel.Children.Add(CreateSectionLabel("Personal Information"));
            var nameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var firstName = WithNeumorphic(new DaisyInput { Label = "First Name", IsRequired = true });
            Grid.SetColumn(firstName, 0);
            nameGrid.Children.Add(firstName);
            var middleName = WithNeumorphic(new DaisyInput { Label = "Middle Name" });
            Grid.SetColumn(middleName, 1);
            nameGrid.Children.Add(middleName);
            var lastName = WithNeumorphic(new DaisyInput { Label = "Last Name", IsRequired = true });
            Grid.SetColumn(lastName, 2);
            nameGrid.Children.Add(lastName);
            panel.Children.Add(nameGrid);

            var dobGenderGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var dob = WithNeumorphic(new DaisyInput { Label = "Date of Birth", Watermark = "MM/DD/YYYY", IsRequired = true });
            Grid.SetColumn(dob, 0);
            dobGenderGrid.Children.Add(dob);
            var gender = WithNeumorphic(new DaisySelect());
            gender.Items.Add("Male"); gender.Items.Add("Female"); gender.Items.Add("Non-binary"); gender.Items.Add("Prefer not to say");
            gender.SelectedIndex = 0;
            var genderLabeled = CreateLabeledSelect("Gender", gender);
            Grid.SetColumn(genderLabeled, 1);
            dobGenderGrid.Children.Add(genderLabeled);
            panel.Children.Add(dobGenderGrid);

            // Contact Info
            panel.Children.Add(CreateSectionLabel("Contact Information"));
            var contactGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var phone = WithNeumorphic(new DaisyInput { Label = "Phone Number", Watermark = "(555) 123-4567", IsRequired = true });
            Grid.SetColumn(phone, 0);
            contactGrid.Children.Add(phone);
            var email = WithNeumorphic(new DaisyInput { Label = "Email Address", Watermark = "patient@email.com" });
            Grid.SetColumn(email, 1);
            contactGrid.Children.Add(email);
            panel.Children.Add(contactGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Street Address", Watermark = "123 Main Street" }));

            // Insurance
            panel.Children.Add(CreateSectionLabel("Insurance Information"));
            var insuranceGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var provider = WithNeumorphic(new DaisyInput { Label = "Insurance Provider" });
            Grid.SetColumn(provider, 0);
            insuranceGrid.Children.Add(provider);
            var memberId = WithNeumorphic(new DaisyInput { Label = "Member ID" });
            Grid.SetColumn(memberId, 1);
            insuranceGrid.Children.Add(memberId);
            panel.Children.Add(insuranceGrid);

            // Medical History
            panel.Children.Add(CreateSectionLabel("Medical History"));
            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Current Medications", Watermark = "List any medications you are currently taking...", MinHeight = 60 }));
            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Allergies", Watermark = "List any known allergies...", MinHeight = 60 }));

            // Consent
            panel.Children.Add(new DaisyDivider { Content = "Consent" });
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I consent to treatment and release of medical information", Variant = DaisyCheckBoxVariant.Primary, IsChecked = false }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I have read and agree to the privacy policy", Variant = DaisyCheckBoxVariant.Primary, IsChecked = false }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Complete Registration", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Cancel", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Real Estate Form
        private static StackPanel BuildRealEstateForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🏠 Property Listing Form"));

            // Property Details
            panel.Children.Add(CreateSectionLabel("Property Details"));
            var typeGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var propType = WithNeumorphic(new DaisySelect());
            propType.Items.Add("Single Family Home"); propType.Items.Add("Condo/Apartment"); propType.Items.Add("Townhouse"); propType.Items.Add("Multi-Family"); propType.Items.Add("Commercial"); propType.Items.Add("Land");
            propType.SelectedIndex = 0;
            var propTypeLabeled = CreateLabeledSelect("Property Type", propType);
            Grid.SetColumn(propTypeLabeled, 0);
            typeGrid.Children.Add(propTypeLabeled);
            var listingType = WithNeumorphic(new DaisySelect());
            listingType.Items.Add("For Sale"); listingType.Items.Add("For Rent"); listingType.Items.Add("Lease-to-Own");
            listingType.SelectedIndex = 0;
            var listingTypeLabeled = CreateLabeledSelect("Listing Type", listingType);
            Grid.SetColumn(listingTypeLabeled, 1);
            typeGrid.Children.Add(listingTypeLabeled);
            panel.Children.Add(typeGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Property Address", Watermark = "123 Oak Street, City, State ZIP", IsRequired = true }));

            var priceGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var price = WithNeumorphic(new DaisyInput { Label = "Asking Price ($)", Watermark = "450,000", IsRequired = true });
            Grid.SetColumn(price, 0);
            priceGrid.Children.Add(price);
            var sqft = WithNeumorphic(new DaisyInput { Label = "Square Footage", Watermark = "2,500" });
            Grid.SetColumn(sqft, 1);
            priceGrid.Children.Add(sqft);
            panel.Children.Add(priceGrid);

            // Rooms
            panel.Children.Add(CreateSectionLabel("Room Details"));
            var roomGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var beds = WithNeumorphic(new DaisySelect());
            for (int i = 0; i <= 10; i++) beds.Items.Add(i.ToString());
            beds.SelectedIndex = 3;
            var bedsLabeled = CreateLabeledSelect("Bedrooms", beds);
            Grid.SetColumn(bedsLabeled, 0);
            roomGrid.Children.Add(bedsLabeled);
            var baths = WithNeumorphic(new DaisySelect());
            baths.Items.Add("1"); baths.Items.Add("1.5"); baths.Items.Add("2"); baths.Items.Add("2.5"); baths.Items.Add("3"); baths.Items.Add("3.5"); baths.Items.Add("4+");
            baths.SelectedIndex = 2;
            var bathsLabeled = CreateLabeledSelect("Bathrooms", baths);
            Grid.SetColumn(bathsLabeled, 1);
            roomGrid.Children.Add(bathsLabeled);
            var garage = WithNeumorphic(new DaisySelect());
            garage.Items.Add("None"); garage.Items.Add("1 Car"); garage.Items.Add("2 Car"); garage.Items.Add("3+ Car");
            garage.SelectedIndex = 0;
            var garageLabeled = CreateLabeledSelect("Garage", garage);
            Grid.SetColumn(garageLabeled, 2);
            roomGrid.Children.Add(garageLabeled);
            var yearBuilt = WithNeumorphic(new DaisyInput { Label = "Year Built", Watermark = "1995" });
            Grid.SetColumn(yearBuilt, 3);
            roomGrid.Children.Add(yearBuilt);
            panel.Children.Add(roomGrid);

            // Features
            panel.Children.Add(CreateSectionLabel("Features & Amenities"));
            var featuresPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            featuresPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Pool", Variant = DaisyCheckBoxVariant.Accent }));
            featuresPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Fireplace", Variant = DaisyCheckBoxVariant.Accent }));
            featuresPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Central A/C", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            featuresPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Hardwood Floors", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(featuresPanel);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Property Description", Watermark = "Describe the property's best features, recent upgrades, neighborhood highlights...", MinHeight = 100 }));

            // Contact
            panel.Children.Add(CreateSectionLabel("Agent/Owner Contact"));
            var agentGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var agentName = WithNeumorphic(new DaisyInput { Label = "Contact Name", IsRequired = true });
            Grid.SetColumn(agentName, 0);
            agentGrid.Children.Add(agentName);
            var agentPhone = WithNeumorphic(new DaisyInput { Label = "Phone", Watermark = "(555) 123-4567" });
            Grid.SetColumn(agentPhone, 1);
            agentGrid.Children.Add(agentPhone);
            panel.Children.Add(agentGrid);

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Publish Listing", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save as Draft", Variant = DaisyButtonVariant.Secondary }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Banking Form
        private static StackPanel BuildBankingForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🏦 Loan Application Form"));

            // Loan Details
            panel.Children.Add(CreateSectionLabel("Loan Details"));
            var loanGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var loanType = WithNeumorphic(new DaisySelect());
            loanType.Items.Add("Personal Loan"); loanType.Items.Add("Auto Loan"); loanType.Items.Add("Home Mortgage"); loanType.Items.Add("Business Loan"); loanType.Items.Add("Student Loan");
            loanType.SelectedIndex = 0;
            var loanTypeLabeled = CreateLabeledSelect("Loan Type", loanType);
            Grid.SetColumn(loanTypeLabeled, 0);
            loanGrid.Children.Add(loanTypeLabeled);
            var loanAmount = WithNeumorphic(new DaisyInput { Label = "Requested Amount ($)", Watermark = "25,000", IsRequired = true });
            Grid.SetColumn(loanAmount, 1);
            loanGrid.Children.Add(loanAmount);
            panel.Children.Add(loanGrid);

            var termGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var term = WithNeumorphic(new DaisySelect());
            term.Items.Add("12 months"); term.Items.Add("24 months"); term.Items.Add("36 months"); term.Items.Add("48 months"); term.Items.Add("60 months"); term.Items.Add("84 months");
            term.SelectedIndex = 0;
            var termLabeled = CreateLabeledSelect("Loan Term", term);
            Grid.SetColumn(termLabeled, 0);
            termGrid.Children.Add(termLabeled);
            var purpose = WithNeumorphic(new DaisyInput { Label = "Loan Purpose", Watermark = "Describe intended use" });
            Grid.SetColumn(purpose, 1);
            termGrid.Children.Add(purpose);
            panel.Children.Add(termGrid);

            // Applicant Information
            panel.Children.Add(CreateSectionLabel("Applicant Information"));
            var nameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var fullName = WithNeumorphic(new DaisyInput { Label = "Full Legal Name", IsRequired = true });
            Grid.SetColumn(fullName, 0);
            nameGrid.Children.Add(fullName);
            var ssn = WithNeumorphic(new DaisyInput { Label = "SSN (last 4 digits)", Watermark = "XXXX" });
            Grid.SetColumn(ssn, 1);
            nameGrid.Children.Add(ssn);
            panel.Children.Add(nameGrid);

            var dobGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var dob = WithNeumorphic(new DaisyInput { Label = "Date of Birth", Watermark = "MM/DD/YYYY", IsRequired = true });
            Grid.SetColumn(dob, 0);
            dobGrid.Children.Add(dob);
            var citizenship = WithNeumorphic(new DaisySelect());
            citizenship.Items.Add("US Citizen"); citizenship.Items.Add("Permanent Resident"); citizenship.Items.Add("Work Visa"); citizenship.Items.Add("Other");
            citizenship.SelectedIndex = 0;
            var citizenshipLabeled = CreateLabeledSelect("Citizenship Status", citizenship);
            Grid.SetColumn(citizenshipLabeled, 1);
            dobGrid.Children.Add(citizenshipLabeled);
            panel.Children.Add(dobGrid);

            // Employment
            panel.Children.Add(CreateSectionLabel("Employment & Income"));
            var empGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var employer = WithNeumorphic(new DaisyInput { Label = "Employer Name" });
            Grid.SetColumn(employer, 0);
            empGrid.Children.Add(employer);
            var income = WithNeumorphic(new DaisyInput { Label = "Annual Income ($)", Watermark = "75,000", IsRequired = true });
            Grid.SetColumn(income, 1);
            empGrid.Children.Add(income);
            panel.Children.Add(empGrid);

            var empStatusGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var empStatus = WithNeumorphic(new DaisySelect());
            empStatus.Items.Add("Full-Time"); empStatus.Items.Add("Part-Time"); empStatus.Items.Add("Self-Employed"); empStatus.Items.Add("Retired"); empStatus.Items.Add("Unemployed");
            empStatus.SelectedIndex = 0;
            var empStatusLabeled = CreateLabeledSelect("Employment Status", empStatus);
            Grid.SetColumn(empStatusLabeled, 0);
            empStatusGrid.Children.Add(empStatusLabeled);
            var yearsEmployed = WithNeumorphic(new DaisyInput { Label = "Years at Current Job", Watermark = "3" });
            Grid.SetColumn(yearsEmployed, 1);
            empStatusGrid.Children.Add(yearsEmployed);
            panel.Children.Add(empStatusGrid);

            // Disclosures
            panel.Children.Add(new DaisyDivider { Content = "Required Disclosures" });
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I authorize a credit check and verification of information", Variant = DaisyCheckBoxVariant.Primary }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I certify that all information provided is true and accurate", Variant = DaisyCheckBoxVariant.Primary }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I have read and agree to the terms and conditions", Variant = DaisyCheckBoxVariant.Primary }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Submit Application", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save Progress", Variant = DaisyButtonVariant.Secondary }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Travel Form
        private static StackPanel BuildTravelForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("✈️ Travel Booking Form"));

            // Trip Details
            panel.Children.Add(CreateSectionLabel("Trip Details"));
            var tripGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var tripType = WithNeumorphic(new DaisySelect());
            tripType.Items.Add("Round Trip"); tripType.Items.Add("One Way"); tripType.Items.Add("Multi-City");
            tripType.SelectedIndex = 0;
            var tripTypeLabeled = CreateLabeledSelect("Trip Type", tripType);
            Grid.SetColumn(tripTypeLabeled, 0);
            tripGrid.Children.Add(tripTypeLabeled);
            var travelers = WithNeumorphic(new DaisySelect());
            for (int i = 1; i <= 10; i++) travelers.Items.Add(i.ToString());
            travelers.SelectedIndex = 0;
            var travelersLabeled = CreateLabeledSelect("Number of Travelers", travelers);
            Grid.SetColumn(travelersLabeled, 1);
            tripGrid.Children.Add(travelersLabeled);
            panel.Children.Add(tripGrid);

            var locGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var origin = WithNeumorphic(new DaisyInput { Label = "Departing From", Watermark = "City or Airport Code", IsRequired = true });
            Grid.SetColumn(origin, 0);
            locGrid.Children.Add(origin);
            var dest = WithNeumorphic(new DaisyInput { Label = "Destination", Watermark = "City or Airport Code", IsRequired = true });
            Grid.SetColumn(dest, 1);
            locGrid.Children.Add(dest);
            panel.Children.Add(locGrid);

            var dateGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var depart = WithNeumorphic(new DaisyInput { Label = "Departure Date", Watermark = "MM/DD/YYYY", IsRequired = true });
            Grid.SetColumn(depart, 0);
            dateGrid.Children.Add(depart);
            var returnDate = WithNeumorphic(new DaisyInput { Label = "Return Date", Watermark = "MM/DD/YYYY" });
            Grid.SetColumn(returnDate, 1);
            dateGrid.Children.Add(returnDate);
            panel.Children.Add(dateGrid);

            // Class & Preferences
            panel.Children.Add(CreateSectionLabel("Preferences"));
            var prefGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var cabinClass = WithNeumorphic(new DaisySelect());
            cabinClass.Items.Add("Economy"); cabinClass.Items.Add("Premium Economy"); cabinClass.Items.Add("Business"); cabinClass.Items.Add("First Class");
            cabinClass.SelectedIndex = 0;
            var cabinClassLabeled = CreateLabeledSelect("Cabin Class", cabinClass);
            Grid.SetColumn(cabinClassLabeled, 0);
            prefGrid.Children.Add(cabinClassLabeled);
            var seatPref = WithNeumorphic(new DaisySelect());
            seatPref.Items.Add("No Preference"); seatPref.Items.Add("Window"); seatPref.Items.Add("Aisle"); seatPref.Items.Add("Extra Legroom");
            seatPref.SelectedIndex = 0;
            var seatPrefLabeled = CreateLabeledSelect("Seat Preference", seatPref);
            Grid.SetColumn(seatPrefLabeled, 1);
            prefGrid.Children.Add(seatPrefLabeled);
            panel.Children.Add(prefGrid);

            var optionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            optionsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Add Hotel", Variant = DaisyCheckBoxVariant.Accent }));
            optionsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Add Car Rental", Variant = DaisyCheckBoxVariant.Accent }));
            optionsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Travel Insurance", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            panel.Children.Add(optionsPanel);

            // Passenger Info
            panel.Children.Add(CreateSectionLabel("Primary Passenger"));
            var passGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var passName = WithNeumorphic(new DaisyInput { Label = "Full Name (as on ID)", IsRequired = true });
            Grid.SetColumn(passName, 0);
            passGrid.Children.Add(passName);
            var passEmail = WithNeumorphic(new DaisyInput { Label = "Email", Watermark = "traveler@email.com", IsRequired = true });
            Grid.SetColumn(passEmail, 1);
            passGrid.Children.Add(passEmail);
            panel.Children.Add(passGrid);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Special Requests", Watermark = "Dietary requirements, wheelchair assistance, etc.", MinHeight = 60 }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Search Flights", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Get Price Alert", Variant = DaisyButtonVariant.Secondary }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Restaurant Form
        private static StackPanel BuildRestaurantForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🍽️ Table Reservation Form"));

            // Reservation Details
            panel.Children.Add(CreateSectionLabel("Reservation Details"));
            var resGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var date = WithNeumorphic(new DaisyInput { Label = "Date", Watermark = "MM/DD/YYYY", IsRequired = true });
            Grid.SetColumn(date, 0);
            resGrid.Children.Add(date);
            var time = WithNeumorphic(new DaisySelect());
            string[] times = ["5:00 PM", "5:30 PM", "6:00 PM", "6:30 PM", "7:00 PM", "7:30 PM", "8:00 PM", "8:30 PM", "9:00 PM"];
            foreach (var t in times) time.Items.Add(t);
            time.SelectedIndex = 4;
            var timeLabeled = CreateLabeledSelect("Time", time);
            Grid.SetColumn(timeLabeled, 1);
            resGrid.Children.Add(timeLabeled);
            var guests = WithNeumorphic(new DaisySelect());
            for (int i = 1; i <= 12; i++) guests.Items.Add(i == 1 ? "1 Guest" : $"{i} Guests");
            guests.SelectedIndex = 1;
            var guestsLabeled = CreateLabeledSelect("Party Size", guests);
            Grid.SetColumn(guestsLabeled, 2);
            resGrid.Children.Add(guestsLabeled);
            panel.Children.Add(resGrid);

            // Seating Preference
            panel.Children.Add(CreateSectionLabel("Seating Preference"));
            var seatingPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            seatingPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Indoor", GroupName = "Seating", IsChecked = true, Variant = DaisyRadioVariant.Primary }));
            seatingPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Outdoor/Patio", GroupName = "Seating", Variant = DaisyRadioVariant.Primary }));
            seatingPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Bar Area", GroupName = "Seating", Variant = DaisyRadioVariant.Primary }));
            seatingPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Private Room", GroupName = "Seating", Variant = DaisyRadioVariant.Primary }));
            panel.Children.Add(seatingPanel);

            // Occasion
            panel.Children.Add(CreateSectionLabel("Special Occasion?"));
            var occasionGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var occasion = WithNeumorphic(new DaisySelect());
            occasion.Items.Add("None"); occasion.Items.Add("Birthday"); occasion.Items.Add("Anniversary"); occasion.Items.Add("Date Night"); occasion.Items.Add("Business Dinner"); occasion.Items.Add("Celebration");
            occasion.SelectedIndex = 0;
            var occasionLabeled = CreateLabeledSelect("Occasion", occasion);
            Grid.SetColumn(occasionLabeled, 0);
            occasionGrid.Children.Add(occasionLabeled);
            panel.Children.Add(occasionGrid);

            // Contact
            panel.Children.Add(CreateSectionLabel("Contact Information"));
            var contactGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var name = WithNeumorphic(new DaisyInput { Label = "Name", IsRequired = true });
            Grid.SetColumn(name, 0);
            contactGrid.Children.Add(name);
            var phone = WithNeumorphic(new DaisyInput { Label = "Phone", Watermark = "(555) 123-4567", IsRequired = true });
            Grid.SetColumn(phone, 1);
            contactGrid.Children.Add(phone);
            panel.Children.Add(contactGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Email", Watermark = "guest@email.com" }));
            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Special Requests", Watermark = "Allergies, dietary restrictions, highchair needed...", MinHeight = 60 }));

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Send me exclusive offers and updates", Variant = DaisyCheckBoxVariant.Accent }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Reserve Table", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "View Menu", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Legal Form
        private static StackPanel BuildLegalForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("⚖️ Case Intake Form"));

            // Case Type
            panel.Children.Add(CreateSectionLabel("Case Information"));
            var caseGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var caseType = WithNeumorphic(new DaisySelect());
            caseType.Items.Add("Personal Injury"); caseType.Items.Add("Family Law"); caseType.Items.Add("Criminal Defense"); caseType.Items.Add("Business/Corporate"); caseType.Items.Add("Real Estate"); caseType.Items.Add("Employment"); caseType.Items.Add("Immigration"); caseType.Items.Add("Estate Planning");
            caseType.SelectedIndex = 0;
            var caseTypeLabeled = CreateLabeledSelect("Case Type", caseType);
            Grid.SetColumn(caseTypeLabeled, 0);
            caseGrid.Children.Add(caseTypeLabeled);
            var urgency = WithNeumorphic(new DaisySelect());
            urgency.Items.Add("Low - General Inquiry"); urgency.Items.Add("Medium - Within 30 days"); urgency.Items.Add("High - Within 7 days"); urgency.Items.Add("Urgent - Immediate");
            urgency.SelectedIndex = 0;
            var urgencyLabeled = CreateLabeledSelect("Urgency Level", urgency);
            Grid.SetColumn(urgencyLabeled, 1);
            caseGrid.Children.Add(urgencyLabeled);
            panel.Children.Add(caseGrid);

            // Client Info
            panel.Children.Add(CreateSectionLabel("Client Information"));
            var nameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var fullName = WithNeumorphic(new DaisyInput { Label = "Full Legal Name", IsRequired = true });
            Grid.SetColumn(fullName, 0);
            nameGrid.Children.Add(fullName);
            var phone = WithNeumorphic(new DaisyInput { Label = "Phone Number", Watermark = "(555) 123-4567", IsRequired = true });
            Grid.SetColumn(phone, 1);
            nameGrid.Children.Add(phone);
            panel.Children.Add(nameGrid);

            var contactGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var email = WithNeumorphic(new DaisyInput { Label = "Email Address", IsRequired = true });
            Grid.SetColumn(email, 0);
            contactGrid.Children.Add(email);
            var preferredContact = WithNeumorphic(new DaisySelect());
            preferredContact.Items.Add("Phone"); preferredContact.Items.Add("Email"); preferredContact.Items.Add("Text Message");
            preferredContact.SelectedIndex = 0;
            var preferredContactLabeled = CreateLabeledSelect("Preferred Contact", preferredContact);
            Grid.SetColumn(preferredContactLabeled, 1);
            contactGrid.Children.Add(preferredContactLabeled);
            panel.Children.Add(contactGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Address", Watermark = "Street, City, State, ZIP" }));

            // Case Details
            panel.Children.Add(CreateSectionLabel("Case Details"));
            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Brief Description of Your Legal Matter", Watermark = "Please describe your situation and what legal assistance you need...", MinHeight = 120, IsRequired = true }));

            var dateGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var incidentDate = WithNeumorphic(new DaisyInput { Label = "Date of Incident (if applicable)", Watermark = "MM/DD/YYYY" });
            Grid.SetColumn(incidentDate, 0);
            dateGrid.Children.Add(incidentDate);
            var deadline = WithNeumorphic(new DaisyInput { Label = "Any Known Deadlines", Watermark = "MM/DD/YYYY" });
            Grid.SetColumn(deadline, 1);
            dateGrid.Children.Add(deadline);
            panel.Children.Add(dateGrid);

            // Prior Representation
            panel.Children.Add(CreateSectionLabel("Prior Representation"));
            var priorPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            priorPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "No prior attorney", GroupName = "Prior", IsChecked = true, Variant = DaisyRadioVariant.Primary }));
            priorPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Currently have an attorney", GroupName = "Prior", Variant = DaisyRadioVariant.Primary }));
            priorPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Previously had an attorney", GroupName = "Prior", Variant = DaisyRadioVariant.Primary }));
            panel.Children.Add(priorPanel);

            // Consent
            panel.Children.Add(new DaisyDivider { Content = "Confidentiality Notice" });
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I understand this form does not create an attorney-client relationship", Variant = DaisyCheckBoxVariant.Primary }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I consent to being contacted regarding my inquiry", Variant = DaisyCheckBoxVariant.Primary }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Request Consultation", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Call Us Now", Variant = DaisyButtonVariant.Secondary }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Education Form
        private static StackPanel BuildEducationForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🎓 Course Enrollment Form"));

            // Course Selection
            panel.Children.Add(CreateSectionLabel("Course Selection"));
            var courseGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var program = WithNeumorphic(new DaisySelect());
            program.Items.Add("Web Development Bootcamp"); program.Items.Add("Data Science Certificate"); program.Items.Add("UX/UI Design"); program.Items.Add("Mobile App Development"); program.Items.Add("Cybersecurity Fundamentals"); program.Items.Add("Cloud Computing"); program.Items.Add("AI & Machine Learning");
            program.SelectedIndex = 0;
            var programLabeled = CreateLabeledSelect("Program", program);
            Grid.SetColumn(programLabeled, 0);
            courseGrid.Children.Add(programLabeled);
            var schedule = WithNeumorphic(new DaisySelect());
            schedule.Items.Add("Full-Time (Mon-Fri)"); schedule.Items.Add("Part-Time Evenings"); schedule.Items.Add("Part-Time Weekends"); schedule.Items.Add("Self-Paced Online");
            schedule.SelectedIndex = 0;
            var scheduleLabeled = CreateLabeledSelect("Schedule", schedule);
            Grid.SetColumn(scheduleLabeled, 1);
            courseGrid.Children.Add(scheduleLabeled);
            panel.Children.Add(courseGrid);

            var startGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var startDate = WithNeumorphic(new DaisySelect());
            startDate.Items.Add("January 2025"); startDate.Items.Add("March 2025"); startDate.Items.Add("June 2025"); startDate.Items.Add("September 2025");
            startDate.SelectedIndex = 0;
            var startDateLabeled = CreateLabeledSelect("Start Date", startDate);
            Grid.SetColumn(startDateLabeled, 0);
            startGrid.Children.Add(startDateLabeled);
            var format = WithNeumorphic(new DaisySelect());
            format.Items.Add("In-Person"); format.Items.Add("Online Live"); format.Items.Add("Hybrid"); format.Items.Add("Self-Paced");
            format.SelectedIndex = 0;
            var formatLabeled = CreateLabeledSelect("Format", format);
            Grid.SetColumn(formatLabeled, 1);
            startGrid.Children.Add(formatLabeled);
            panel.Children.Add(startGrid);

            // Student Info
            panel.Children.Add(CreateSectionLabel("Student Information"));
            var nameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var firstName = WithNeumorphic(new DaisyInput { Label = "First Name", IsRequired = true });
            Grid.SetColumn(firstName, 0);
            nameGrid.Children.Add(firstName);
            var lastName = WithNeumorphic(new DaisyInput { Label = "Last Name", IsRequired = true });
            Grid.SetColumn(lastName, 1);
            nameGrid.Children.Add(lastName);
            panel.Children.Add(nameGrid);

            var contactGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var email = WithNeumorphic(new DaisyInput { Label = "Email", Watermark = "student@email.com", IsRequired = true });
            Grid.SetColumn(email, 0);
            contactGrid.Children.Add(email);
            var phone = WithNeumorphic(new DaisyInput { Label = "Phone", Watermark = "(555) 123-4567" });
            Grid.SetColumn(phone, 1);
            contactGrid.Children.Add(phone);
            panel.Children.Add(contactGrid);

            // Background
            panel.Children.Add(CreateSectionLabel("Educational Background"));
            var eduGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var highestEdu = WithNeumorphic(new DaisySelect());
            highestEdu.Items.Add("High School"); highestEdu.Items.Add("Some College"); highestEdu.Items.Add("Associate's Degree"); highestEdu.Items.Add("Bachelor's Degree"); highestEdu.Items.Add("Master's Degree"); highestEdu.Items.Add("Doctorate");
            highestEdu.SelectedIndex = 0;
            var highestEduLabeled = CreateLabeledSelect("Highest Education", highestEdu);
            Grid.SetColumn(highestEduLabeled, 0);
            eduGrid.Children.Add(highestEduLabeled);
            var experience = WithNeumorphic(new DaisySelect());
            experience.Items.Add("None"); experience.Items.Add("Hobbyist/Self-taught"); experience.Items.Add("1-2 years"); experience.Items.Add("3-5 years"); experience.Items.Add("5+ years");
            experience.SelectedIndex = 0;
            var experienceLabeled = CreateLabeledSelect("Prior Experience", experience);
            Grid.SetColumn(experienceLabeled, 1);
            eduGrid.Children.Add(experienceLabeled);
            panel.Children.Add(eduGrid);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Why are you interested in this program?", Watermark = "Tell us about your goals and what you hope to achieve...", MinHeight = 80 }));

            // Payment
            panel.Children.Add(CreateSectionLabel("Payment Options"));
            var paymentPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            paymentPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Pay in Full", GroupName = "Payment", IsChecked = true, Variant = DaisyRadioVariant.Primary }));
            paymentPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Monthly Installments", GroupName = "Payment", Variant = DaisyRadioVariant.Primary }));
            paymentPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Employer Sponsorship", GroupName = "Payment", Variant = DaisyRadioVariant.Primary }));
            panel.Children.Add(paymentPanel);

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I'm interested in financing options", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I agree to the enrollment terms and refund policy", Variant = DaisyCheckBoxVariant.Primary }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Submit Application", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Download Syllabus", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region JobBoard Form
        private static StackPanel BuildJobBoardForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("💼 Job Application Form"));

            // Position
            panel.Children.Add(CreateSectionLabel("Position Details"));
            var posGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var position = WithNeumorphic(new DaisyInput { Label = "Position Applied For", Watermark = "e.g., Senior Software Engineer", IsRequired = true });
            Grid.SetColumn(position, 0);
            posGrid.Children.Add(position);
            var department = WithNeumorphic(new DaisySelect());
            department.Items.Add("Engineering"); department.Items.Add("Product"); department.Items.Add("Design"); department.Items.Add("Marketing"); department.Items.Add("Sales"); department.Items.Add("Operations"); department.Items.Add("HR"); department.Items.Add("Finance");
            department.SelectedIndex = 0;
            var departmentLabeled = CreateLabeledSelect("Department", department);
            Grid.SetColumn(departmentLabeled, 1);
            posGrid.Children.Add(departmentLabeled);
            panel.Children.Add(posGrid);

            // Personal Info
            panel.Children.Add(CreateSectionLabel("Personal Information"));
            var nameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var fullName = WithNeumorphic(new DaisyInput { Label = "Full Name", IsRequired = true });
            Grid.SetColumn(fullName, 0);
            nameGrid.Children.Add(fullName);
            var email = WithNeumorphic(new DaisyInput { Label = "Email", Watermark = "applicant@email.com", IsRequired = true });
            Grid.SetColumn(email, 1);
            nameGrid.Children.Add(email);
            panel.Children.Add(nameGrid);

            var contactGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var phone = WithNeumorphic(new DaisyInput { Label = "Phone Number", Watermark = "(555) 123-4567" });
            Grid.SetColumn(phone, 0);
            contactGrid.Children.Add(phone);
            var location = WithNeumorphic(new DaisyInput { Label = "Current Location", Watermark = "City, State/Country" });
            Grid.SetColumn(location, 1);
            contactGrid.Children.Add(location);
            panel.Children.Add(contactGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "LinkedIn Profile", Watermark = "https://linkedin.com/in/yourprofile" }));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Portfolio/Website", Watermark = "https://yourportfolio.com" }));

            // Experience
            panel.Children.Add(CreateSectionLabel("Experience & Qualifications"));
            var expGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var yearsExp = WithNeumorphic(new DaisySelect());
            yearsExp.Items.Add("Entry Level (0-1)"); yearsExp.Items.Add("Junior (1-3)"); yearsExp.Items.Add("Mid-Level (3-5)"); yearsExp.Items.Add("Senior (5-8)"); yearsExp.Items.Add("Lead (8-12)"); yearsExp.Items.Add("Principal (12+)");
            yearsExp.SelectedIndex = 0;
            var yearsExpLabeled = CreateLabeledSelect("Years of Experience", yearsExp);
            Grid.SetColumn(yearsExpLabeled, 0);
            expGrid.Children.Add(yearsExpLabeled);
            var education = WithNeumorphic(new DaisySelect());
            education.Items.Add("High School"); education.Items.Add("Associate's"); education.Items.Add("Bachelor's"); education.Items.Add("Master's"); education.Items.Add("Doctorate"); education.Items.Add("Bootcamp/Certification");
            education.SelectedIndex = 0;
            var educationLabeled = CreateLabeledSelect("Highest Education", education);
            Grid.SetColumn(educationLabeled, 1);
            expGrid.Children.Add(educationLabeled);
            panel.Children.Add(expGrid);

            panel.Children.Add(CreateLabeledFileInput("Resume/CV", WithNeumorphic(new DaisyFileInput())));
            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Cover Letter / Why This Role?", Watermark = "Tell us why you're interested in this position and what makes you a great fit...", MinHeight = 100 }));

            // Availability
            panel.Children.Add(CreateSectionLabel("Availability"));
            var availGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var availStartDate = WithNeumorphic(new DaisySelect());
            availStartDate.Items.Add("Immediately"); availStartDate.Items.Add("2 weeks notice"); availStartDate.Items.Add("1 month"); availStartDate.Items.Add("2+ months");
            availStartDate.SelectedIndex = 0;
            var availStartDateLabeled = CreateLabeledSelect("Available to Start", availStartDate);
            Grid.SetColumn(availStartDateLabeled, 0);
            availGrid.Children.Add(availStartDateLabeled);
            var salary = WithNeumorphic(new DaisyInput { Label = "Expected Salary", Watermark = "$XX,XXX - $XX,XXX" });
            Grid.SetColumn(salary, 1);
            availGrid.Children.Add(salary);
            panel.Children.Add(availGrid);

            var workPrefPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            workPrefPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Remote", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            workPrefPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Hybrid", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            workPrefPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "On-Site", Variant = DaisyCheckBoxVariant.Accent }));
            workPrefPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Open to Relocation", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(workPrefPanel);

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I certify that all information provided is accurate", Variant = DaisyCheckBoxVariant.Primary }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Submit Application", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save as Draft", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Hotel Form
        private static StackPanel BuildHotelForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🏨 Guest Check-In Form"));

            // Reservation
            panel.Children.Add(CreateSectionLabel("Reservation Details"));
            var resGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var confNumber = WithNeumorphic(new DaisyInput { Label = "Confirmation Number", Watermark = "ABC123456", IsRequired = true });
            Grid.SetColumn(confNumber, 0);
            resGrid.Children.Add(confNumber);
            var roomType = WithNeumorphic(new DaisySelect());
            roomType.Items.Add("Standard King"); roomType.Items.Add("Standard Double Queen"); roomType.Items.Add("Deluxe King"); roomType.Items.Add("Junior Suite"); roomType.Items.Add("Executive Suite"); roomType.Items.Add("Presidential Suite");
            roomType.SelectedIndex = 0;
            var roomTypeLabeled = CreateLabeledSelect("Room Type", roomType);
            Grid.SetColumn(roomTypeLabeled, 1);
            resGrid.Children.Add(roomTypeLabeled);
            panel.Children.Add(resGrid);

            var dateGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var checkIn = WithNeumorphic(new DaisyInput { Label = "Check-In Date", Watermark = "MM/DD/YYYY", IsRequired = true });
            Grid.SetColumn(checkIn, 0);
            dateGrid.Children.Add(checkIn);
            var checkOut = WithNeumorphic(new DaisyInput { Label = "Check-Out Date", Watermark = "MM/DD/YYYY", IsRequired = true });
            Grid.SetColumn(checkOut, 1);
            dateGrid.Children.Add(checkOut);
            var guestsHotel = WithNeumorphic(new DaisySelect());
            for (int i = 1; i <= 6; i++) guestsHotel.Items.Add(i.ToString());
            guestsHotel.SelectedIndex = 0;
            var guestsHotelLabeled = CreateLabeledSelect("Number of Guests", guestsHotel);
            Grid.SetColumn(guestsHotelLabeled, 2);
            dateGrid.Children.Add(guestsHotelLabeled);
            panel.Children.Add(dateGrid);

            // Guest Info
            panel.Children.Add(CreateSectionLabel("Primary Guest Information"));
            var nameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var fullName = WithNeumorphic(new DaisyInput { Label = "Full Name (as on ID)", IsRequired = true });
            Grid.SetColumn(fullName, 0);
            nameGrid.Children.Add(fullName);
            var idType = WithNeumorphic(new DaisySelect());
            idType.Items.Add("Driver's License"); idType.Items.Add("Passport"); idType.Items.Add("State ID"); idType.Items.Add("Military ID");
            idType.SelectedIndex = 0;
            var idTypeLabeled = CreateLabeledSelect("ID Type", idType);
            Grid.SetColumn(idTypeLabeled, 1);
            nameGrid.Children.Add(idTypeLabeled);
            panel.Children.Add(nameGrid);

            var contactGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var email = WithNeumorphic(new DaisyInput { Label = "Email", Watermark = "guest@email.com", IsRequired = true });
            Grid.SetColumn(email, 0);
            contactGrid.Children.Add(email);
            var phone = WithNeumorphic(new DaisyInput { Label = "Phone", Watermark = "(555) 123-4567", IsRequired = true });
            Grid.SetColumn(phone, 1);
            contactGrid.Children.Add(phone);
            panel.Children.Add(contactGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Address", Watermark = "Street, City, State, ZIP, Country" }));

            // Preferences
            panel.Children.Add(CreateSectionLabel("Room Preferences"));
            var prefPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            prefPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Non-Smoking", GroupName = "Smoking", IsChecked = true, Variant = DaisyRadioVariant.Primary }));
            prefPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Smoking", GroupName = "Smoking", Variant = DaisyRadioVariant.Primary }));
            panel.Children.Add(prefPanel);

            var floorPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            floorPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Low Floor", GroupName = "Floor", Variant = DaisyRadioVariant.Accent }));
            floorPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "High Floor", GroupName = "Floor", IsChecked = true, Variant = DaisyRadioVariant.Accent }));
            floorPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "No Preference", GroupName = "Floor", Variant = DaisyRadioVariant.Accent }));
            panel.Children.Add(floorPanel);

            // Extras
            panel.Children.Add(CreateSectionLabel("Additional Services"));
            var extrasPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            extrasPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Breakfast Included", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            extrasPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Airport Shuttle", Variant = DaisyCheckBoxVariant.Accent }));
            extrasPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Late Check-Out", Variant = DaisyCheckBoxVariant.Accent }));
            extrasPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Spa Access", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(extrasPanel);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Special Requests", Watermark = "Feather-free pillows, extra towels, celebration decorations...", MinHeight = 60 }));

            // Payment
            panel.Children.Add(CreateSectionLabel("Payment Information"));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Credit Card (for incidentals)", Watermark = "**** **** **** ****" }));

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I agree to the hotel policies and terms of stay", Variant = DaisyCheckBoxVariant.Primary }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Complete Check-In", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Request Room Change", Variant = DaisyButtonVariant.Secondary }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Fitness Form
        private static StackPanel BuildFitnessForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("💪 Membership Registration Form"));

            // Membership Type
            panel.Children.Add(CreateSectionLabel("Membership Options"));
            var memberPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            memberPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Monthly ($49/mo)", GroupName = "Membership", Variant = DaisyRadioVariant.Primary }));
            memberPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Annual ($39/mo)", GroupName = "Membership", IsChecked = true, Variant = DaisyRadioVariant.Primary }));
            memberPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Premium ($79/mo)", GroupName = "Membership", Variant = DaisyRadioVariant.Primary }));
            panel.Children.Add(memberPanel);

            // Personal Info
            panel.Children.Add(CreateSectionLabel("Personal Information"));
            var nameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var firstName = WithNeumorphic(new DaisyInput { Label = "First Name", IsRequired = true });
            Grid.SetColumn(firstName, 0);
            nameGrid.Children.Add(firstName);
            var lastName = WithNeumorphic(new DaisyInput { Label = "Last Name", IsRequired = true });
            Grid.SetColumn(lastName, 1);
            nameGrid.Children.Add(lastName);
            panel.Children.Add(nameGrid);

            var contactGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var email = WithNeumorphic(new DaisyInput { Label = "Email", Watermark = "member@email.com", IsRequired = true });
            Grid.SetColumn(email, 0);
            contactGrid.Children.Add(email);
            var phone = WithNeumorphic(new DaisyInput { Label = "Phone", Watermark = "(555) 123-4567", IsRequired = true });
            Grid.SetColumn(phone, 1);
            contactGrid.Children.Add(phone);
            panel.Children.Add(contactGrid);

            var dobGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var dob = WithNeumorphic(new DaisyInput { Label = "Date of Birth", Watermark = "MM/DD/YYYY", IsRequired = true });
            Grid.SetColumn(dob, 0);
            dobGrid.Children.Add(dob);
            var genderFitness = WithNeumorphic(new DaisySelect());
            genderFitness.Items.Add("Male"); genderFitness.Items.Add("Female"); genderFitness.Items.Add("Non-binary"); genderFitness.Items.Add("Prefer not to say");
            genderFitness.SelectedIndex = 0;
            var genderFitnessLabeled = CreateLabeledSelect("Gender", genderFitness);
            Grid.SetColumn(genderFitnessLabeled, 1);
            dobGrid.Children.Add(genderFitnessLabeled);
            panel.Children.Add(dobGrid);

            // Emergency Contact
            panel.Children.Add(CreateSectionLabel("Emergency Contact"));
            var emergGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var emergName = WithNeumorphic(new DaisyInput { Label = "Contact Name", IsRequired = true });
            Grid.SetColumn(emergName, 0);
            emergGrid.Children.Add(emergName);
            var emergPhone = WithNeumorphic(new DaisyInput { Label = "Phone", Watermark = "(555) 123-4567" });
            Grid.SetColumn(emergPhone, 1);
            emergGrid.Children.Add(emergPhone);
            var relationship = WithNeumorphic(new DaisySelect());
            relationship.Items.Add("Spouse"); relationship.Items.Add("Parent"); relationship.Items.Add("Sibling"); relationship.Items.Add("Friend"); relationship.Items.Add("Other");
            relationship.SelectedIndex = 0;
            var relationshipLabeled = CreateLabeledSelect("Relationship", relationship);
            Grid.SetColumn(relationshipLabeled, 2);
            emergGrid.Children.Add(relationshipLabeled);
            panel.Children.Add(emergGrid);

            // Fitness Goals
            panel.Children.Add(CreateSectionLabel("Fitness Goals"));
            var goalsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            goalsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Weight Loss", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            goalsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Muscle Building", Variant = DaisyCheckBoxVariant.Accent }));
            goalsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "General Fitness", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            goalsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Flexibility", Variant = DaisyCheckBoxVariant.Accent }));
            goalsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Sports Training", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(goalsPanel);

            // Add-ons
            panel.Children.Add(CreateSectionLabel("Add-On Services"));
            var addonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            addonsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Personal Training Sessions", Variant = DaisyCheckBoxVariant.Accent }));
            addonsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Group Classes Access", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            addonsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Locker Rental", Variant = DaisyCheckBoxVariant.Accent }));
            addonsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Towel Service", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(addonsPanel);

            // Health Waiver
            panel.Children.Add(new DaisyDivider { Content = "Health & Liability" });
            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Known Health Conditions", Watermark = "List any injuries, conditions, or limitations we should be aware of...", MinHeight = 60 }));

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I confirm I am physically able to participate in fitness activities", Variant = DaisyCheckBoxVariant.Primary }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I have read and agree to the liability waiver and gym rules", Variant = DaisyCheckBoxVariant.Primary }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Start Membership", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Schedule Tour", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Ecommerce Form
        private static StackPanel BuildEcommerceForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🛒 Checkout Form"));

            // Shipping Address
            panel.Children.Add(CreateSectionLabel("Shipping Address"));
            var nameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var firstName = WithNeumorphic(new DaisyInput { Label = "First Name", IsRequired = true });
            Grid.SetColumn(firstName, 0);
            nameGrid.Children.Add(firstName);
            var lastName = WithNeumorphic(new DaisyInput { Label = "Last Name", IsRequired = true });
            Grid.SetColumn(lastName, 1);
            nameGrid.Children.Add(lastName);
            panel.Children.Add(nameGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Company (optional)", Watermark = "Company name" }));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Street Address", Watermark = "123 Main Street", IsRequired = true }));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Apartment, suite, etc.", Watermark = "Apt 4B" }));

            var cityGrid = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }, new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var city = WithNeumorphic(new DaisyInput { Label = "City", IsRequired = true });
            Grid.SetColumn(city, 0);
            cityGrid.Children.Add(city);
            var state = WithNeumorphic(new DaisySelect());
            state.Items.Add("CA"); state.Items.Add("NY"); state.Items.Add("TX"); state.Items.Add("FL"); state.Items.Add("WA"); state.Items.Add("Other...");
            state.SelectedIndex = 0;
            var stateLabeled = CreateLabeledSelect("State", state);
            Grid.SetColumn(stateLabeled, 1);
            cityGrid.Children.Add(stateLabeled);
            var zip = WithNeumorphic(new DaisyInput { Label = "ZIP Code", Watermark = "12345", IsRequired = true });
            Grid.SetColumn(zip, 2);
            cityGrid.Children.Add(zip);
            panel.Children.Add(cityGrid);

            var contactGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var email = WithNeumorphic(new DaisyInput { Label = "Email", Watermark = "you@email.com", IsRequired = true });
            Grid.SetColumn(email, 0);
            contactGrid.Children.Add(email);
            var phone = WithNeumorphic(new DaisyInput { Label = "Phone", Watermark = "(555) 123-4567" });
            Grid.SetColumn(phone, 1);
            contactGrid.Children.Add(phone);
            panel.Children.Add(contactGrid);

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Save this address for future orders", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));

            // Shipping Method
            panel.Children.Add(CreateSectionLabel("Shipping Method"));
            var shipPanel = new StackPanel { Spacing = 8 };
            shipPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Standard Shipping (5-7 business days) — FREE", GroupName = "Shipping", IsChecked = true, Variant = DaisyRadioVariant.Primary }));
            shipPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Express Shipping (2-3 business days) — $9.99", GroupName = "Shipping", Variant = DaisyRadioVariant.Primary }));
            shipPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Overnight Shipping (1 business day) — $24.99", GroupName = "Shipping", Variant = DaisyRadioVariant.Primary }));
            panel.Children.Add(shipPanel);

            // Payment
            panel.Children.Add(CreateSectionLabel("Payment Information"));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Card Number", Watermark = "1234 5678 9012 3456", IsRequired = true }));

            var payGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var expiry = WithNeumorphic(new DaisyInput { Label = "Expiry Date", Watermark = "MM/YY", IsRequired = true });
            Grid.SetColumn(expiry, 0);
            payGrid.Children.Add(expiry);
            var cvv = WithNeumorphic(new DaisyInput { Label = "CVV", Watermark = "123", IsRequired = true });
            Grid.SetColumn(cvv, 1);
            payGrid.Children.Add(cvv);
            var cardName = WithNeumorphic(new DaisyInput { Label = "Name on Card", IsRequired = true });
            Grid.SetColumn(cardName, 2);
            payGrid.Children.Add(cardName);
            panel.Children.Add(payGrid);

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Billing address same as shipping", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));

            // Promo
            panel.Children.Add(CreateSectionLabel("Promo Code"));
            var promoGrid = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }, new ColumnDefinition() }, ColumnSpacing = 12 };
            var promoCode = WithNeumorphic(new DaisyInput { Watermark = "Enter promo code" });
            Grid.SetColumn(promoCode, 0);
            promoGrid.Children.Add(promoCode);
            var applyBtn = WithNeumorphic(new DaisyButton { Content = "Apply", Variant = DaisyButtonVariant.Secondary });
            Grid.SetColumn(applyBtn, 1);
            promoGrid.Children.Add(applyBtn);
            panel.Children.Add(promoGrid);

            // Gift
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "This is a gift (include gift message)", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Subscribe to newsletter for exclusive offers", Variant = DaisyCheckBoxVariant.Accent }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Place Order", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Continue Shopping", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Gaming Form
        private static StackPanel BuildGamingForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🎮 Player Profile & Setup"));

            panel.Children.Add(CreateSectionLabel("Account Information"));
            var gameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var gamerTag = WithNeumorphic(new DaisyInput { Label = "GamerTag", Watermark = "NoobSlayer99", IsRequired = true });
            Grid.SetColumn(gamerTag, 0);
            gameGrid.Children.Add(gamerTag);
            var platform = WithNeumorphic(new DaisySelect());
            platform.Items.Add("PC (Windows)"); platform.Items.Add("PlayStation 5"); platform.Items.Add("Xbox Series X/S"); platform.Items.Add("Nintendo Switch"); platform.Items.Add("Mobile");
            platform.SelectedIndex = 0;
            var platformLabeled = CreateLabeledSelect("Primary Platform", platform);
            Grid.SetColumn(platformLabeled, 1);
            gameGrid.Children.Add(platformLabeled);
            panel.Children.Add(gameGrid);

            panel.Children.Add(CreateSectionLabel("Gaming Preferences"));
            var genrePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            genrePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "FPS", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            genrePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "RPG", Variant = DaisyCheckBoxVariant.Accent }));
            genrePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "MOBA", Variant = DaisyCheckBoxVariant.Accent }));
            genrePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Strategy", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(genrePanel);

            var serverRegion = WithNeumorphic(new DaisySelect());
            serverRegion.Items.Add("North America"); serverRegion.Items.Add("Europe"); serverRegion.Items.Add("Asia"); serverRegion.Items.Add("South America"); serverRegion.Items.Add("Oceania");
            serverRegion.SelectedIndex = 0;
            panel.Children.Add(CreateLabeledSelect("Server Region", serverRegion));

            panel.Children.Add(CreateSectionLabel("Streaming & Social"));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Twitch URL", Watermark = "twitch.tv/username" }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Enable cross-play with other platforms", Variant = DaisyCheckBoxVariant.Primary, IsChecked = true }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Show my online status to friends", Variant = DaisyCheckBoxVariant.Primary, IsChecked = true }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save Profile", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Join Discord", Variant = DaisyButtonVariant.Secondary }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Creative/Agency Form
        private static StackPanel BuildCreativeForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("Project Inquiry Brief"));

            panel.Children.Add(CreateSectionLabel("Project Overview"));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Project Name", Watermark = "e.g., Brand Identity Refresh", IsRequired = true }));

            var serviceGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var service = WithNeumorphic(new DaisySelect());
            service.Items.Add("Graphic Design"); service.Items.Add("Web Development"); service.Items.Add("Marketing Strategy"); service.Items.Add("UI/UX Design"); service.Items.Add("Video Production");
            service.SelectedIndex = 0;
            var serviceLabeled = CreateLabeledSelect("Service Required", service);
            Grid.SetColumn(serviceLabeled, 0);
            serviceGrid.Children.Add(serviceLabeled);
            var budget = WithNeumorphic(new DaisySelect());
            budget.Items.Add("$1k - $5k"); budget.Items.Add("$5k - $15k"); budget.Items.Add("$15k - $50k"); budget.Items.Add("$50k+");
            budget.SelectedIndex = 0;
            var budgetLabeled = CreateLabeledSelect("Budget Range", budget);
            Grid.SetColumn(budgetLabeled, 1);
            serviceGrid.Children.Add(budgetLabeled);
            panel.Children.Add(serviceGrid);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "What are your goals for this project?", Watermark = "Describe your vision, target audience, and key deliverables...", MinHeight = 80 }));

            panel.Children.Add(CreateSectionLabel("Brand Direction"));
            var vibePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            vibePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Minimalist", Variant = DaisyCheckBoxVariant.Accent }));
            vibePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Bold & Vibrant", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            vibePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Professional/Corporate", Variant = DaisyCheckBoxVariant.Accent }));
            vibePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Playful", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(vibePanel);

            panel.Children.Add(CreateLabeledFileInput("Inspiration / Existing Assets (Moodboard, Logo, etc.)", WithNeumorphic(new DaisyFileInput())));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Submit Inquiry", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "View Our Work", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region AI/Chatbot Form
        private static StackPanel BuildAIForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🤖 AI Model Configuration"));

            panel.Children.Add(CreateSectionLabel("Model Settings"));
            var modelGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var model = WithNeumorphic(new DaisySelect());
            model.Items.Add("GPT-4o"); model.Items.Add("Claude 3.5 Sonnet"); model.Items.Add("Llama 3.1 70B"); model.Items.Add("Gemini 1.5 Pro");
            model.SelectedIndex = 0;
            var modelLabeled = CreateLabeledSelect("Base Model", model);
            Grid.SetColumn(modelLabeled, 0);
            modelGrid.Children.Add(modelLabeled);
            var temperature = WithNeumorphic(new DaisyInput { Label = "Temperature (0.0 - 2.0)", Watermark = "0.7" });
            Grid.SetColumn(temperature, 1);
            modelGrid.Children.Add(temperature);
            panel.Children.Add(modelGrid);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "System Prompt / Instructions", Watermark = "You are a helpful assistant specialized in coding and software architecture...", MinHeight = 100 }));

            panel.Children.Add(CreateSectionLabel("Safety & Filters"));
            panel.Children.Add(WithNeumorphic(new DaisyToggle { Content = "Enable Content Filtering", IsChecked = true, Variant = DaisyToggleVariant.Success }));
            panel.Children.Add(WithNeumorphic(new DaisyToggle { Content = "Log All Interactions", IsChecked = false, Variant = DaisyToggleVariant.Primary }));

            panel.Children.Add(CreateSectionLabel("API Integration"));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "API Key", Watermark = "sk-....", Variant = DaisyInputVariant.Bordered }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save Settings", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Test Connection", Variant = DaisyButtonVariant.Secondary }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Security Form
        private static StackPanel BuildSecurityForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🛡️ Security Audit Request"));

            panel.Children.Add(CreateSectionLabel("Target Infrastructure"));
            var targetGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var targetType = WithNeumorphic(new DaisySelect());
            targetType.Items.Add("Web Application"); targetType.Items.Add("Network/Infrastructure"); targetType.Items.Add("Cloud (AWS/Azure/GCP)"); targetType.Items.Add("Mobile App (iOS/Android)"); targetType.Items.Add("Smart Contract");
            targetType.SelectedIndex = 0;
            var targetTypeLabeled = CreateLabeledSelect("Audit Type", targetType);
            Grid.SetColumn(targetTypeLabeled, 0);
            targetGrid.Children.Add(targetTypeLabeled);
            var severity = WithNeumorphic(new DaisySelect());
            severity.Items.Add("Routine Audit"); severity.Items.Add("Active Incident Response"); severity.Items.Add("Compliance Requirement");
            severity.SelectedIndex = 0;
            var severityLabeled = CreateLabeledSelect("Urgency Level", severity);
            Grid.SetColumn(severityLabeled, 1);
            targetGrid.Children.Add(severityLabeled);
            panel.Children.Add(targetGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Target Domain / IP Range", Watermark = "https://api.example.com", IsRequired = true }));

            panel.Children.Add(CreateSectionLabel("Security Compliance"));
            var compPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            compPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "SOC2", Variant = DaisyCheckBoxVariant.Accent }));
            compPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "HIPAA", Variant = DaisyCheckBoxVariant.Accent }));
            compPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "GDPR", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            compPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "PCI-DSS", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(compPanel);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Access Details / Known Vulnerabilities", Watermark = "Provide any specific areas of concern or credentials for authenticated scanning...", MinHeight = 80 }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Schedule Audit", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Download NDA", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region SpaceTech Form
        private static StackPanel BuildSpaceTechForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🚀 Mission Launch Checklist"));

            panel.Children.Add(CreateSectionLabel("Mission Parameters"));
            var missionGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var orbit = WithNeumorphic(new DaisySelect());
            orbit.Items.Add("LEO (Low Earth)"); orbit.Items.Add("MEO (Medium Earth)"); orbit.Items.Add("GEO (Geostationary)"); orbit.Items.Add("Lunar"); orbit.Items.Add("Interplanetary");
            orbit.SelectedIndex = 0;
            var orbitLabeled = CreateLabeledSelect("Target Orbit", orbit);
            Grid.SetColumn(orbitLabeled, 0);
            missionGrid.Children.Add(orbitLabeled);
            var vehicle = WithNeumorphic(new DaisySelect());
            vehicle.Items.Add("Heavy Lifter X-1"); vehicle.Items.Add("Reusable Scout-9"); vehicle.Items.Add("Micro-Satellite Deployer");
            vehicle.SelectedIndex = 0;
            var vehicleLabeled = CreateLabeledSelect("Launch Vehicle", vehicle);
            Grid.SetColumn(vehicleLabeled, 1);
            missionGrid.Children.Add(vehicleLabeled);
            panel.Children.Add(missionGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Payload Mass (kg)", Watermark = "5,400", IsRequired = true }));

            panel.Children.Add(CreateSectionLabel("Pre-Launch Status"));
            panel.Children.Add(WithNeumorphic(new DaisyToggle { Content = "Telemetry Sync Confirmed", IsChecked = true, Variant = DaisyToggleVariant.Success }));
            panel.Children.Add(WithNeumorphic(new DaisyToggle { Content = "Fueling Sequence Initialized", IsChecked = false, Variant = DaisyToggleVariant.Warning }));
            panel.Children.Add(WithNeumorphic(new DaisyToggle { Content = "Guidance System Calibrated", IsChecked = true, Variant = DaisyToggleVariant.Success }));

            panel.Children.Add(CreateSectionLabel("Payload Description"));
            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Watermark = "Communication satellite array for global broadband connectivity...", MinHeight = 60 }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Finalize Flight Plan", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Abort Sequence", Variant = DaisyButtonVariant.Error }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Dating Form
        private static StackPanel BuildDatingForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("❤️ Love & Connection Profile"));

            panel.Children.Add(CreateSectionLabel("About You"));
            var bioGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var age = WithNeumorphic(new DaisyInput { Label = "Age", Watermark = "28", IsRequired = true });
            Grid.SetColumn(age, 0);
            bioGrid.Children.Add(age);
            var identity = WithNeumorphic(new DaisySelect());
            identity.Items.Add("Man"); identity.Items.Add("Woman"); identity.Items.Add("Non-binary"); identity.Items.Add("Agender");
            identity.SelectedIndex = 0;
            var identityLabeled = CreateLabeledSelect("Identity", identity);
            Grid.SetColumn(identityLabeled, 1);
            bioGrid.Children.Add(identityLabeled);
            panel.Children.Add(bioGrid);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "My Perfect Sunday", Watermark = "Waking up late, grabbing coffee at the local market, and a long walk in the park...", MinHeight = 80 }));

            panel.Children.Add(CreateSectionLabel("What are you looking for?"));
            var interestPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            interestPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Long-term", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            interestPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Short-term", Variant = DaisyCheckBoxVariant.Accent }));
            interestPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "New Friends", Variant = DaisyCheckBoxVariant.Accent }));
            interestPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Marriage", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(interestPanel);

            panel.Children.Add(CreateSectionLabel("Interests & Hobbies"));
            var hobbies = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            hobbies.Children.Add(WithNeumorphic(new DaisyBadge { Content = "Cooking", Variant = DaisyBadgeVariant.Primary }));
            hobbies.Children.Add(WithNeumorphic(new DaisyBadge { Content = "Travel", Variant = DaisyBadgeVariant.Secondary }));
            hobbies.Children.Add(WithNeumorphic(new DaisyBadge { Content = "Gaming", Variant = DaisyBadgeVariant.Accent }));
            hobbies.Children.Add(WithNeumorphic(new DaisyBadge { Content = "Fitness", Variant = DaisyBadgeVariant.Info }));
            panel.Children.Add(hobbies);

            panel.Children.Add(CreateLabeledFileInput("Upload Profile Photos (Max 6)", WithNeumorphic(new DaisyFileInput())));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Publish Profile", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Preview", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Pet Tech Form
        private static StackPanel BuildPetForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🐾 Pet Health Profile"));

            panel.Children.Add(CreateSectionLabel("Pet Information"));
            var petGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var petName = WithNeumorphic(new DaisyInput { Label = "Pet's Name", Watermark = "Buddy", IsRequired = true });
            Grid.SetColumn(petName, 0);
            petGrid.Children.Add(petName);
            var species = WithNeumorphic(new DaisySelect());
            species.Items.Add("Dog"); species.Items.Add("Cat"); species.Items.Add("Rabbit"); species.Items.Add("Bird"); species.Items.Add("Other");
            species.SelectedIndex = 0;
            var speciesLabeled = CreateLabeledSelect("Species", species);
            Grid.SetColumn(speciesLabeled, 1);
            petGrid.Children.Add(speciesLabeled);
            panel.Children.Add(petGrid);

            var breedGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var breed = WithNeumorphic(new DaisyInput { Label = "Breed", Watermark = "Golden Retriever" });
            Grid.SetColumn(breed, 0);
            breedGrid.Children.Add(breed);
            var age = WithNeumorphic(new DaisyInput { Label = "Age (years)", Watermark = "3" });
            Grid.SetColumn(age, 1);
            breedGrid.Children.Add(age);
            panel.Children.Add(breedGrid);

            panel.Children.Add(CreateSectionLabel("Medical History"));
            var medicalPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            medicalPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Vaccinated", Variant = DaisyCheckBoxVariant.Success, IsChecked = true }));
            medicalPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Microchipped", Variant = DaisyCheckBoxVariant.Success, IsChecked = true }));
            medicalPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Neutered/Spayed", Variant = DaisyCheckBoxVariant.Success }));
            panel.Children.Add(medicalPanel);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Allergies or Special Needs", Watermark = "Sensitive stomach, allergic to grain, fearful of loud noises...", MinHeight = 60 }));

            panel.Children.Add(WithNeumorphic(new DaisyToggle { Content = "Enable Smart Tracker Notifications", IsChecked = true, Variant = DaisyToggleVariant.Primary }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save Pet Profile", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Book Vet Appointment", Variant = DaisyButtonVariant.Secondary }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Sustainability/Environment Form
        private static StackPanel BuildSustainabilityForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🌍 Carbon Footprint Calculator"));

            panel.Children.Add(CreateSectionLabel("Business Operations"));
            var energyGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var energy = WithNeumorphic(new DaisyInput { Label = "Monthly Electricity (kWh)", Watermark = "1,200", IsRequired = true });
            Grid.SetColumn(energy, 0);
            energyGrid.Children.Add(energy);
            var fuel = WithNeumorphic(new DaisyInput { Label = "Fleet Fuel (Liters)", Watermark = "450" });
            Grid.SetColumn(fuel, 1);
            energyGrid.Children.Add(fuel);
            panel.Children.Add(energyGrid);

            panel.Children.Add(CreateSectionLabel("Waste & Recycling"));
            var wastePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            wastePanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Paper Recycling", GroupName = "Recycle", IsChecked = true, Variant = DaisyRadioVariant.Primary }));
            wastePanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Plastic/Metal", GroupName = "Recycle", Variant = DaisyRadioVariant.Primary }));
            wastePanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Composting", GroupName = "Recycle", Variant = DaisyRadioVariant.Primary }));
            panel.Children.Add(wastePanel);

            panel.Children.Add(CreateSectionLabel("ESG Goals"));
            var goalGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var target = WithNeumorphic(new DaisySelect());
            target.Items.Add("10% by 2026"); target.Items.Add("25% by 2030"); target.Items.Add("Net Zero by 2040"); target.Items.Add("Carbon Negative");
            target.SelectedIndex = 0;
            var targetLabeled = CreateLabeledSelect("Target Reduction", target);
            Grid.SetColumn(targetLabeled, 0);
            goalGrid.Children.Add(targetLabeled);
            panel.Children.Add(goalGrid);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Recent Green Initiatives", Watermark = "Installed solar panels, moved to remote-first policy, optimized shipping routes...", MinHeight = 80 }));

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Receive quarterly sustainability impact report", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Calculate Impact", Variant = DaisyButtonVariant.Success }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Download Certification", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Non-Profit Form
        private static StackPanel BuildNonProfitForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🤝 Volunteer Application"));

            panel.Children.Add(CreateSectionLabel("Contact Info"));
            var nameGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var name = WithNeumorphic(new DaisyInput { Label = "Full Name", IsRequired = true });
            Grid.SetColumn(name, 0);
            nameGrid.Children.Add(name);
            var email = WithNeumorphic(new DaisyInput { Label = "Email", Watermark = "volunteer@email.org", IsRequired = true });
            Grid.SetColumn(email, 1);
            nameGrid.Children.Add(email);
            panel.Children.Add(nameGrid);

            panel.Children.Add(CreateSectionLabel("Availability & Interests"));
            var availability = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            availability.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Weekdays", Variant = DaisyCheckBoxVariant.Primary }));
            availability.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Weekends", Variant = DaisyCheckBoxVariant.Primary, IsChecked = true }));
            availability.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Evenings", Variant = DaisyCheckBoxVariant.Primary }));
            panel.Children.Add(availability);

            var interest = WithNeumorphic(new DaisySelect());
            interest.Items.Add("Community Outreach"); interest.Items.Add("Event Coordination"); interest.Items.Add("Administrative Support"); interest.Items.Add("Tutoring/Mentoring"); interest.Items.Add("Environmental Cleanup");
            interest.SelectedIndex = 0;
            panel.Children.Add(CreateLabeledSelect("Primary Interest Area", interest));

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Why do you want to volunteer with us?", Watermark = "Share your motivation and any relevant skills or experience...", MinHeight = 100 }));

            panel.Children.Add(CreateSectionLabel("Donation (Optional)"));
            var donationGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var amount = WithNeumorphic(new DaisyInput { Label = "One-time Donation ($)", Watermark = "25.00" });
            Grid.SetColumn(amount, 0);
            donationGrid.Children.Add(amount);
            panel.Children.Add(donationGrid);

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(new DaisyButton { Content = "Submit Application", Variant = DaisyButtonVariant.Primary });
            buttonRow.Children.Add(new DaisyButton { Content = "Our Mission", Variant = DaisyButtonVariant.Ghost });
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Sports Team Form
        private static StackPanel BuildSportsForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("⚽ Team Registration"));

            panel.Children.Add(CreateSectionLabel("Team Details"));
            var teamGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var teamName = WithNeumorphic(new DaisyInput { Label = "Team Name", Watermark = "The Thunderbolts", IsRequired = true });
            Grid.SetColumn(teamName, 0);
            teamGrid.Children.Add(teamName);
            var sport = WithNeumorphic(new DaisySelect());
            sport.Items.Add("Soccer"); sport.Items.Add("Basketball"); sport.Items.Add("Baseball"); sport.Items.Add("Volleyball"); sport.Items.Add("Tennis");
            sport.SelectedIndex = 0;
            var sportLabeled = CreateLabeledSelect("Sport", sport);
            Grid.SetColumn(sportLabeled, 1);
            teamGrid.Children.Add(sportLabeled);
            panel.Children.Add(teamGrid);

            var division = WithNeumorphic(new DaisySelect());
            division.Items.Add("Under 12 (Beginner)"); division.Items.Add("Under 16 (Intermediate)"); division.Items.Add("Adult Amateur"); division.Items.Add("Pro-Am / Competitive");
            division.SelectedIndex = 0;
            panel.Children.Add(CreateLabeledSelect("Division / Skill Level", division));

            panel.Children.Add(CreateSectionLabel("Team Logistics"));
            var colorGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var primaryColor = WithNeumorphic(new DaisyInput { Label = "Primary Jersey Color", Watermark = "Navy Blue" });
            Grid.SetColumn(primaryColor, 0);
            colorGrid.Children.Add(primaryColor);
            var secondaryColor = WithNeumorphic(new DaisyInput { Label = "Secondary Color", Watermark = "Gold" });
            Grid.SetColumn(secondaryColor, 1);
            colorGrid.Children.Add(secondaryColor);
            panel.Children.Add(colorGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Home Field / Venue", Watermark = "Central Community Park" }));

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "We require equipment rental", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I have read the league liability waiver", Variant = DaisyCheckBoxVariant.Primary }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Register Team", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Season Schedule", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Arts & Entertainment Form
        private static StackPanel BuildArtsForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🎭 Event & Exhibition Booking"));

            panel.Children.Add(CreateSectionLabel("Exhibition Details"));
            var artGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var eventType = WithNeumorphic(new DaisySelect());
            eventType.Items.Add("Art Gallery Opening"); eventType.Items.Add("Theater Performance"); eventType.Items.Add("Concert/Live Music"); eventType.Items.Add("Film Screening"); eventType.Items.Add("Poetry Slam");
            eventType.SelectedIndex = 0;
            var eventTypeLabeled = CreateLabeledSelect("Event Type", eventType);
            Grid.SetColumn(eventTypeLabeled, 0);
            artGrid.Children.Add(eventTypeLabeled);
            var date = WithNeumorphic(new DaisyInput { Label = "Requested Date", Watermark = "MM/DD/YYYY", IsRequired = true });
            Grid.SetColumn(date, 1);
            artGrid.Children.Add(date);
            panel.Children.Add(artGrid);

            panel.Children.Add(CreateSectionLabel("Venue Requirements"));
            var spacePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            spacePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Stage Lights", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            spacePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Sound System", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            spacePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Projector/Screen", Variant = DaisyCheckBoxVariant.Accent }));
            spacePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Catering Tables", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(spacePanel);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Artist/Exhibition Statement", Watermark = "Tell us about the work being presented and its technical requirements...", MinHeight = 100 }));

            panel.Children.Add(CreateSectionLabel("Tickets & Pricing"));
            var ticketGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var price = WithNeumorphic(new DaisyInput { Label = "Standard Ticket Price ($)", Watermark = "15.00" });
            Grid.SetColumn(price, 0);
            ticketGrid.Children.Add(price);
            var capacity = WithNeumorphic(new DaisyInput { Label = "Target Capacity", Watermark = "150" });
            Grid.SetColumn(capacity, 1);
            ticketGrid.Children.Add(capacity);
            panel.Children.Add(ticketGrid);

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Submit Proposal", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Venue Gallery", Variant = DaisyButtonVariant.Secondary }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Design Form
        private static StackPanel BuildDesignForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("📐 Design Project Questionnaire"));

            panel.Children.Add(CreateSectionLabel("Client Intent"));
            var designGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var projectType = WithNeumorphic(new DaisySelect());
            projectType.Items.Add("Residential Interior"); projectType.Items.Add("Commercial/Office"); projectType.Items.Add("Architecture (New Build)"); projectType.Items.Add("Renovation/Remodel");
            projectType.SelectedIndex = 0;
            var projectTypeLabeled = CreateLabeledSelect("Project Category", projectType);
            Grid.SetColumn(projectTypeLabeled, 0);
            designGrid.Children.Add(projectTypeLabeled);
            var stylePref = WithNeumorphic(new DaisySelect());
            stylePref.Items.Add("Modern/Minimalist"); stylePref.Items.Add("Industrial"); stylePref.Items.Add("Mid-Century Modern"); stylePref.Items.Add("Traditional"); stylePref.Items.Add("Eclectic");
            stylePref.SelectedIndex = 0;
            var stylePrefLabeled = CreateLabeledSelect("Style Preference", stylePref);
            Grid.SetColumn(stylePrefLabeled, 1);
            designGrid.Children.Add(stylePrefLabeled);
            panel.Children.Add(designGrid);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Space Description & Key Goals", Watermark = "Describe the current space and your vision for its transformation...", MinHeight = 100 }));

            panel.Children.Add(CreateSectionLabel("Technical Specifications"));
            var techGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var sqft = WithNeumorphic(new DaisyInput { Label = "Total Area (sq ft)", Watermark = "1,500" });
            Grid.SetColumn(sqft, 0);
            techGrid.Children.Add(sqft);
            var timeline = WithNeumorphic(new DaisySelect());
            timeline.Items.Add("Within 3 months"); timeline.Items.Add("3-6 months"); timeline.Items.Add("6-12 months"); timeline.Items.Add("12+ months");
            timeline.SelectedIndex = 0;
            var timelineLabeled = CreateLabeledSelect("Target Completion", timeline);
            Grid.SetColumn(timelineLabeled, 1);
            techGrid.Children.Add(timelineLabeled);
            panel.Children.Add(techGrid);

            panel.Children.Add(CreateLabeledFileInput("Site Photos / Floor Plans / Blueprints", WithNeumorphic(new DaisyFileInput())));

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I have a preferred contractor/builder already", Variant = DaisyCheckBoxVariant.Primary }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Request Estimate", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "View Our Work", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region ERP Form
        private static StackPanel BuildERPForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("📋 Customer Master Data"));

            // Customer Identification
            panel.Children.Add(CreateSectionLabel("Customer Identification"));
            var idGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var customerId = WithNeumorphic(new DaisyInput { Label = "Customer ID", Watermark = "Auto-generated", IsEnabled = false });
            Grid.SetColumn(customerId, 0);
            idGrid.Children.Add(customerId);
            var customerType = WithNeumorphic(new DaisySelect());
            customerType.Items.Add("Individual"); customerType.Items.Add("Business"); customerType.Items.Add("Government"); customerType.Items.Add("Non-Profit");
            customerType.SelectedIndex = 0;
            var customerTypeLabeled = CreateLabeledSelect("Customer Type", customerType);
            Grid.SetColumn(customerTypeLabeled, 1);
            idGrid.Children.Add(customerTypeLabeled);
            var status = WithNeumorphic(new DaisySelect());
            status.Items.Add("Active"); status.Items.Add("Inactive"); status.Items.Add("Prospect"); status.Items.Add("On Hold");
            status.SelectedIndex = 0;
            var statusLabeled = CreateLabeledSelect("Status", status);
            Grid.SetColumn(statusLabeled, 2);
            idGrid.Children.Add(statusLabeled);
            panel.Children.Add(idGrid);

            // Company / Individual Details
            panel.Children.Add(CreateSectionLabel("Company / Contact Details"));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Company Name / Full Name", IsRequired = true }));
            var contactGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var contactPerson = WithNeumorphic(new DaisyInput { Label = "Primary Contact Person", Watermark = "John Smith" });
            Grid.SetColumn(contactPerson, 0);
            contactGrid.Children.Add(contactPerson);
            var jobTitle = WithNeumorphic(new DaisyInput { Label = "Job Title", Watermark = "Procurement Manager" });
            Grid.SetColumn(jobTitle, 1);
            contactGrid.Children.Add(jobTitle);
            panel.Children.Add(contactGrid);

            var phoneEmailGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var phone = WithNeumorphic(new DaisyInput { Label = "Phone Number", Watermark = "+1 (555) 123-4567", IsRequired = true });
            Grid.SetColumn(phone, 0);
            phoneEmailGrid.Children.Add(phone);
            var email = WithNeumorphic(new DaisyInput { Label = "Email Address", Watermark = "contact@company.com", IsRequired = true });
            Grid.SetColumn(email, 1);
            phoneEmailGrid.Children.Add(email);
            panel.Children.Add(phoneEmailGrid);

            // Billing Address
            panel.Children.Add(CreateSectionLabel("Billing Address"));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Street Address Line 1", Watermark = "123 Business Park Drive", IsRequired = true }));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Street Address Line 2", Watermark = "Suite 400" }));
            var cityStateGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var city = WithNeumorphic(new DaisyInput { Label = "City", Watermark = "New York", IsRequired = true });
            Grid.SetColumn(city, 0);
            cityStateGrid.Children.Add(city);
            var state = WithNeumorphic(new DaisyInput { Label = "State/Province", Watermark = "NY" });
            Grid.SetColumn(state, 1);
            cityStateGrid.Children.Add(state);
            var zip = WithNeumorphic(new DaisyInput { Label = "ZIP/Postal Code", Watermark = "10001" });
            Grid.SetColumn(zip, 2);
            cityStateGrid.Children.Add(zip);
            panel.Children.Add(cityStateGrid);

            var country = WithNeumorphic(new DaisySelect());
            country.Items.Add("United States"); country.Items.Add("Canada"); country.Items.Add("United Kingdom"); country.Items.Add("Germany"); country.Items.Add("France"); country.Items.Add("Australia"); country.Items.Add("Other");
            country.SelectedIndex = 0;
            panel.Children.Add(CreateLabeledSelect("Country", country));

            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Shipping address same as billing", Variant = DaisyCheckBoxVariant.Primary, IsChecked = true }));

            // Financial / Credit Terms
            panel.Children.Add(CreateSectionLabel("Financial Terms"));
            var finGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 12 };
            var paymentTerms = WithNeumorphic(new DaisySelect());
            paymentTerms.Items.Add("Net 15"); paymentTerms.Items.Add("Net 30"); paymentTerms.Items.Add("Net 45"); paymentTerms.Items.Add("Net 60"); paymentTerms.Items.Add("Due on Receipt"); paymentTerms.Items.Add("COD");
            paymentTerms.SelectedIndex = 1;
            var paymentTermsLabeled = CreateLabeledSelect("Payment Terms", paymentTerms);
            Grid.SetColumn(paymentTermsLabeled, 0);
            finGrid.Children.Add(paymentTermsLabeled);
            var creditLimit = WithNeumorphic(new DaisyInput { Label = "Credit Limit ($)", Watermark = "10,000.00" });
            Grid.SetColumn(creditLimit, 1);
            finGrid.Children.Add(creditLimit);
            var currency = WithNeumorphic(new DaisySelect());
            currency.Items.Add("USD"); currency.Items.Add("EUR"); currency.Items.Add("GBP"); currency.Items.Add("CAD"); currency.Items.Add("AUD");
            currency.SelectedIndex = 0;
            var currencyLabeled = CreateLabeledSelect("Currency", currency);
            Grid.SetColumn(currencyLabeled, 2);
            finGrid.Children.Add(currencyLabeled);
            panel.Children.Add(finGrid);

            var taxGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var taxId = WithNeumorphic(new DaisyInput { Label = "Tax ID / VAT Number", Watermark = "XX-XXXXXXX" });
            Grid.SetColumn(taxId, 0);
            taxGrid.Children.Add(taxId);
            var taxExempt = WithNeumorphic(new DaisySelect());
            taxExempt.Items.Add("Taxable"); taxExempt.Items.Add("Tax Exempt"); taxExempt.Items.Add("Partial Exemption");
            taxExempt.SelectedIndex = 0;
            var taxExemptLabeled = CreateLabeledSelect("Tax Status", taxExempt);
            Grid.SetColumn(taxExemptLabeled, 1);
            taxGrid.Children.Add(taxExemptLabeled);
            panel.Children.Add(taxGrid);

            // Sales Assignment
            panel.Children.Add(CreateSectionLabel("Sales Assignment"));
            var salesGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var salesRep = WithNeumorphic(new DaisySelect());
            salesRep.Items.Add("Unassigned"); salesRep.Items.Add("John Anderson"); salesRep.Items.Add("Sarah Mitchell"); salesRep.Items.Add("Michael Chen"); salesRep.Items.Add("Emily Rodriguez");
            salesRep.SelectedIndex = 0;
            var salesRepLabeled = CreateLabeledSelect("Sales Representative", salesRep);
            Grid.SetColumn(salesRepLabeled, 0);
            salesGrid.Children.Add(salesRepLabeled);
            var territory = WithNeumorphic(new DaisySelect());
            territory.Items.Add("Northeast"); territory.Items.Add("Southeast"); territory.Items.Add("Midwest"); territory.Items.Add("Southwest"); territory.Items.Add("West Coast"); territory.Items.Add("International");
            territory.SelectedIndex = 0;
            var territoryLabeled = CreateLabeledSelect("Sales Territory", territory);
            Grid.SetColumn(territoryLabeled, 1);
            salesGrid.Children.Add(territoryLabeled);
            panel.Children.Add(salesGrid);

            // Notes
            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Internal Notes", Watermark = "Add any relevant notes about this customer account...", MinHeight = 80 }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save Customer", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save & Add Another", Variant = DaisyButtonVariant.Secondary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Cancel", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region Media Form
        private static StackPanel BuildMediaForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("📰 Content Submission Form"));

            // Article Details
            panel.Children.Add(CreateSectionLabel("Content Details"));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Title / Headline", Watermark = "Enter a compelling headline...", IsRequired = true }));

            var typeGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var contentType = WithNeumorphic(new DaisySelect());
            contentType.Items.Add("Article"); contentType.Items.Add("Blog Post"); contentType.Items.Add("Newsletter"); contentType.Items.Add("Press Release"); contentType.Items.Add("Opinion/Editorial"); contentType.Items.Add("Interview");
            contentType.SelectedIndex = 0;
            var contentTypeLabeled = CreateLabeledSelect("Content Type", contentType);
            Grid.SetColumn(contentTypeLabeled, 0);
            typeGrid.Children.Add(contentTypeLabeled);
            var category = WithNeumorphic(new DaisySelect());
            category.Items.Add("Technology"); category.Items.Add("Business"); category.Items.Add("Lifestyle"); category.Items.Add("Entertainment"); category.Items.Add("Politics"); category.Items.Add("Sports"); category.Items.Add("Science");
            category.SelectedIndex = 0;
            var categoryLabeled = CreateLabeledSelect("Category", category);
            Grid.SetColumn(categoryLabeled, 1);
            typeGrid.Children.Add(categoryLabeled);
            panel.Children.Add(typeGrid);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Summary / Excerpt", Watermark = "Write a brief summary (150-200 words) that will appear in previews...", MinHeight = 80, IsRequired = true }));

            // Author Information
            panel.Children.Add(CreateSectionLabel("Author Information"));
            var authorGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var authorName = WithNeumorphic(new DaisyInput { Label = "Author Name", IsRequired = true });
            Grid.SetColumn(authorName, 0);
            authorGrid.Children.Add(authorName);
            var authorEmail = WithNeumorphic(new DaisyInput { Label = "Author Email", Watermark = "author@publication.com" });
            Grid.SetColumn(authorEmail, 1);
            authorGrid.Children.Add(authorEmail);
            panel.Children.Add(authorGrid);

            panel.Children.Add(WithNeumorphic(new DaisyTextArea { Label = "Author Bio", Watermark = "Brief author biography for the byline...", MinHeight = 60 }));

            // Publishing Options
            panel.Children.Add(CreateSectionLabel("Publishing Options"));
            var pubGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var pubStatus = WithNeumorphic(new DaisySelect());
            pubStatus.Items.Add("Draft"); pubStatus.Items.Add("Pending Review"); pubStatus.Items.Add("Scheduled"); pubStatus.Items.Add("Published");
            pubStatus.SelectedIndex = 0;
            var pubStatusLabeled = CreateLabeledSelect("Status", pubStatus);
            Grid.SetColumn(pubStatusLabeled, 0);
            pubGrid.Children.Add(pubStatusLabeled);
            var pubDate = WithNeumorphic(new DaisyInput { Label = "Publish Date", Watermark = "MM/DD/YYYY HH:MM" });
            Grid.SetColumn(pubDate, 1);
            pubGrid.Children.Add(pubDate);
            panel.Children.Add(pubGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Tags (comma-separated)", Watermark = "technology, innovation, startup, AI" }));

            var optionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            optionsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Featured Article", Variant = DaisyCheckBoxVariant.Accent }));
            optionsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Allow Comments", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            optionsPanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Send to Newsletter", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(optionsPanel);

            // Media Attachments
            panel.Children.Add(CreateSectionLabel("Media Attachments"));
            panel.Children.Add(CreateLabeledFileInput("Featured Image", WithNeumorphic(new DaisyFileInput())));
            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Image Caption", Watermark = "Describe the featured image..." }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Submit for Review", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Save Draft", Variant = DaisyButtonVariant.Secondary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Preview", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        #region SaaS Form (Default)
        private static StackPanel BuildSaaSForm()
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(CreateFormTitle("🚀 SaaS Onboarding Form"));

            // Account Setup
            panel.Children.Add(CreateSectionLabel("Account Setup"));
            var emailGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var email = WithNeumorphic(new DaisyInput { Label = "Work Email", Watermark = "you@company.com", IsRequired = true });
            Grid.SetColumn(email, 0);
            emailGrid.Children.Add(email);
            var password = WithNeumorphic(new DaisyInput { Label = "Password", Watermark = "Create a strong password", IsRequired = true });
            Grid.SetColumn(password, 1);
            emailGrid.Children.Add(password);
            panel.Children.Add(emailGrid);

            // Company Info
            panel.Children.Add(CreateSectionLabel("Company Information"));
            var compGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var companyName = WithNeumorphic(new DaisyInput { Label = "Company Name", IsRequired = true });
            Grid.SetColumn(companyName, 0);
            compGrid.Children.Add(companyName);
            var companySize = WithNeumorphic(new DaisySelect());
            companySize.Items.Add("1-10 employees"); companySize.Items.Add("11-50 employees"); companySize.Items.Add("51-200 employees"); companySize.Items.Add("201-500 employees"); companySize.Items.Add("500+ employees");
            companySize.SelectedIndex = 0;
            var companySizeLabeled = CreateLabeledSelect("Company Size", companySize);
            Grid.SetColumn(companySizeLabeled, 1);
            compGrid.Children.Add(companySizeLabeled);
            panel.Children.Add(compGrid);

            var industryGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var industry = WithNeumorphic(new DaisySelect());
            industry.Items.Add("Technology"); industry.Items.Add("Finance"); industry.Items.Add("Healthcare"); industry.Items.Add("Education"); industry.Items.Add("Retail"); industry.Items.Add("Manufacturing"); industry.Items.Add("Other");
            industry.SelectedIndex = 0;
            var industryLabeled = CreateLabeledSelect("Industry", industry);
            Grid.SetColumn(industryLabeled, 0);
            industryGrid.Children.Add(industryLabeled);
            var role = WithNeumorphic(new DaisySelect());
            role.Items.Add("CEO/Founder"); role.Items.Add("CTO/VP Engineering"); role.Items.Add("Product Manager"); role.Items.Add("Developer"); role.Items.Add("Designer"); role.Items.Add("Marketing"); role.Items.Add("Sales"); role.Items.Add("Other");
            role.SelectedIndex = 0;
            var roleLabeled = CreateLabeledSelect("Your Role", role);
            Grid.SetColumn(roleLabeled, 1);
            industryGrid.Children.Add(roleLabeled);
            panel.Children.Add(industryGrid);

            panel.Children.Add(WithNeumorphic(new DaisyInput { Label = "Company Website", Watermark = "https://yourcompany.com" }));

            // Use Case
            panel.Children.Add(CreateSectionLabel("How will you use our product?"));
            var usePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            usePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Project Management", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            usePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Team Collaboration", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));
            usePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Analytics & Reporting", Variant = DaisyCheckBoxVariant.Accent }));
            usePanel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Customer Support", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(usePanel);

            var usePanel2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            usePanel2.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Sales & CRM", Variant = DaisyCheckBoxVariant.Accent }));
            usePanel2.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Marketing Automation", Variant = DaisyCheckBoxVariant.Accent }));
            usePanel2.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "DevOps/CI-CD", Variant = DaisyCheckBoxVariant.Accent }));
            usePanel2.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Other", Variant = DaisyCheckBoxVariant.Accent }));
            panel.Children.Add(usePanel2);

            // Plan
            panel.Children.Add(CreateSectionLabel("Select Your Plan"));
            var planPanel = new StackPanel { Spacing = 8 };
            planPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Free — Up to 3 users, basic features", GroupName = "Plan", IsChecked = true, Variant = DaisyRadioVariant.Primary }));
            planPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Pro — $15/user/month, advanced features", GroupName = "Plan", Variant = DaisyRadioVariant.Primary }));
            planPanel.Children.Add(WithNeumorphic(new DaisyRadio { Content = "Enterprise — Custom pricing, dedicated support", GroupName = "Plan", Variant = DaisyRadioVariant.Primary }));
            panel.Children.Add(planPanel);

            // Referral
            panel.Children.Add(CreateSectionLabel("How did you hear about us?"));
            var referralGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 16 };
            var referral = WithNeumorphic(new DaisySelect());
            referral.Items.Add("Google Search"); referral.Items.Add("Social Media"); referral.Items.Add("Friend/Colleague"); referral.Items.Add("Blog/Article"); referral.Items.Add("Conference/Event"); referral.Items.Add("Other");
            referral.SelectedIndex = 0;
            var referralLabeled = CreateLabeledSelect("Source", referral);
            Grid.SetColumn(referralLabeled, 0);
            referralGrid.Children.Add(referralLabeled);
            var refCode = WithNeumorphic(new DaisyInput { Label = "Referral Code (optional)", Watermark = "FRIEND20" });
            Grid.SetColumn(refCode, 1);
            referralGrid.Children.Add(refCode);
            panel.Children.Add(referralGrid);

            // Terms
            panel.Children.Add(new DaisyDivider());
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "I agree to the Terms of Service and Privacy Policy", Variant = DaisyCheckBoxVariant.Primary }));
            panel.Children.Add(WithNeumorphic(new DaisyCheckBox { Content = "Send me product updates and tips (you can unsubscribe anytime)", Variant = DaisyCheckBoxVariant.Accent, IsChecked = true }));

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Create Account", Variant = DaisyButtonVariant.Primary }));
            buttonRow.Children.Add(WithNeumorphic(new DaisyButton { Content = "Talk to Sales", Variant = DaisyButtonVariant.Ghost }));
            panel.Children.Add(buttonRow);

            return panel;
        }
        #endregion

        public void ScrollToSection(string sectionName)
        {
            var target = GetSectionTarget(sectionName);
            if (target == null)
                return;

            var transform = target.TransformToVisual(MainScrollViewer);
            if (transform.HasValue)
            {
                var point = transform.Value.Transform(new Point(0, 0));
                MainScrollViewer.Offset = new Vector(0, point.Y + MainScrollViewer.Offset.Y);
            }
        }

        private Visual? GetSectionTarget(string sectionId)
        {
            if (_sectionTargetsById == null)
            {
                _sectionTargetsById = new Dictionary<string, Visual>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in this.GetVisualDescendants().OfType<SectionHeader>())
                {
                    if (!string.IsNullOrWhiteSpace(header.SectionId))
                        _sectionTargetsById[header.SectionId] = header.Parent as Visual ?? header;
                }
            }

            return _sectionTargetsById.TryGetValue(sectionId, out var target) ? target : null;
        }
    }
}


