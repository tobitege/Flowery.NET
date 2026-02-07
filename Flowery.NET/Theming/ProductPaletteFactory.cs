using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using Flowery.Helpers;

namespace Flowery.Theming
{
    /// <summary>
    /// Holds product-type specific color values for themed palettes.
    /// Based on industry-standard color combinations for different product categories.
    /// </summary>
    public sealed class ProductPalette
    {
        public required string Name { get; init; }
        public required KeywordList Keywords { get; init; }
        public required string Primary { get; init; }
        public required string Secondary { get; init; }
        public required string Accent { get; init; }
        public required string Background { get; init; }
        public required string Text { get; init; }
        public required string Border { get; init; }
        public string? Notes { get; init; }
    }

    /// <summary>
    /// Non-generic keyword container for API surfaces.
    /// </summary>
    public sealed class KeywordList : IEnumerable
    {
        private readonly List<string> _keywords = [];

        public KeywordList()
        {
        }

        public KeywordList(IEnumerable keywords)
        {
            AddRange(keywords);
        }

        public int Count => _keywords.Count;

        public string this[int index] => _keywords[index];

        public void Add(string keyword)
        {
            if (!string.IsNullOrWhiteSpace(keyword))
                _keywords.Add(keyword);
        }

        public void AddRange(IEnumerable keywords)
        {
            if (keywords == null)
                return;

            foreach (var item in keywords)
            {
                if (item is string keyword)
                    Add(keyword);
            }
        }

        public bool Contains(string keyword, StringComparison comparison)
        {
            foreach (var entry in _keywords)
            {
                if (string.Equals(entry, keyword, comparison))
                    return true;
            }

            return false;
        }

        public IEnumerator GetEnumerator() => _keywords.GetEnumerator();
    }

    /// <summary>
    /// Factory for creating product-type themed palettes.
    /// Contains 96 industry-specific color schemes that can be joined with DaisyPaletteFactory at runtime.
    /// </summary>
    public static class ProductPaletteFactory
    {
        private static readonly List<ProductPalette> ProductPalettes =
        [
            // SaaS & Tech Platforms
            new ProductPalette { Name = "SaaS", Keywords = new KeywordList(new[] { "saas", "general" }), Primary = "#2563EB", Secondary = "#3B82F6", Accent = "#F97316", Background = "#F8FAFC", Text = "#1E293B", Border = "#E2E8F0", Notes = "Trust blue + accent contrast" },
            new ProductPalette { Name = "MicroSaaS", Keywords = new KeywordList(new[] { "micro", "saas" }), Primary = "#6366F1", Secondary = "#818CF8", Accent = "#10B981", Background = "#F8FAFC", Text = "#1E293B", Border = "#E2E8F0", Notes = "Vibrant indigo + green success" },
            new ProductPalette { Name = "Ecommerce", Keywords = new KeywordList(new[] { "commerce" }), Primary = "#3B82F6", Secondary = "#60A5FA", Accent = "#16A34A", Background = "#F8FAFC", Text = "#1E293B", Border = "#E2E8F0", Notes = "Brand primary + success green" },
            new ProductPalette { Name = "EcommerceLuxury", Keywords = new KeywordList(new[] { "commerce", "luxury" }), Primary = "#1C1917", Secondary = "#44403C", Accent = "#CA8A04", Background = "#FAFAF9", Text = "#0C0A09", Border = "#D6D3D1", Notes = "Premium colors + minimal accent" },
            new ProductPalette { Name = "ServiceLanding", Keywords = new KeywordList(new[] { "service", "landing", "page" }), Primary = "#0EA5E9", Secondary = "#38BDF8", Accent = "#F97316", Background = "#F8FAFC", Text = "#1E293B", Border = "#E2E8F0", Notes = "Brand primary + trust colors" },
            new ProductPalette { Name = "B2BService", Keywords = new KeywordList(new[] { "b2b", "service" }), Primary = "#0F172A", Secondary = "#334155", Accent = "#0369A1", Background = "#F8FAFC", Text = "#020617", Border = "#E2E8F0", Notes = "Professional blue + neutral grey" },
            new ProductPalette { Name = "FinancialDashboard", Keywords = new KeywordList(new[] { "financial", "dashboard" }), Primary = "#1E3A5F", Secondary = "#2563EB", Accent = "#DC2626", Background = "#0F172A", Text = "#F1F5F9", Border = "#334155", Notes = "Dark bg + red/green alerts + trust blue" },
            new ProductPalette { Name = "AnalyticsDashboard", Keywords = new KeywordList(new[] { "analytics", "dashboard" }), Primary = "#6366F1", Secondary = "#8B5CF6", Accent = "#F59E0B", Background = "#111827", Text = "#F9FAFB", Border = "#374151", Notes = "Cool→Hot gradients + neutral grey" },
            new ProductPalette { Name = "HealthcareApp", Keywords = new KeywordList(new[] { "healthcare", "app" }), Primary = "#0891B2", Secondary = "#22D3EE", Accent = "#059669", Background = "#ECFEFF", Text = "#164E63", Border = "#A5F3FC", Notes = "Calm blue + health green + trust" },
            new ProductPalette { Name = "EducationalApp", Keywords = new KeywordList(new[] { "educational", "app" }), Primary = "#4F46E5", Secondary = "#818CF8", Accent = "#F97316", Background = "#EEF2FF", Text = "#1E1B4B", Border = "#C7D2FE", Notes = "Playful colors + clear hierarchy" },
            new ProductPalette { Name = "CreativeAgency", Keywords = new KeywordList(new[] { "creative", "agency" }), Primary = "#EC4899", Secondary = "#F472B6", Accent = "#06B6D4", Background = "#FDF2F8", Text = "#831843", Border = "#FBCFE8", Notes = "Bold primaries + artistic freedom" },
            new ProductPalette { Name = "Portfolio", Keywords = new KeywordList(new[] { "portfolio", "personal" }), Primary = "#18181B", Secondary = "#3F3F46", Accent = "#2563EB", Background = "#FAFAFA", Text = "#09090B", Border = "#E4E4E7", Notes = "Brand primary + artistic interpretation" },
            new ProductPalette { Name = "Gaming", Keywords = new KeywordList(new[] { "gaming" }), Primary = "#7C3AED", Secondary = "#A78BFA", Accent = "#F43F5E", Background = "#0F0F23", Text = "#E2E8F0", Border = "#4C1D95", Notes = "Vibrant + neon + immersive colors" },
            new ProductPalette { Name = "Government", Keywords = new KeywordList(new[] { "government", "public", "service" }), Primary = "#0F172A", Secondary = "#334155", Accent = "#0369A1", Background = "#F8FAFC", Text = "#020617", Border = "#E2E8F0", Notes = "Professional blue + high contrast" },
            new ProductPalette { Name = "FintechCrypto", Keywords = new KeywordList(new[] { "fintech", "crypto" }), Primary = "#F59E0B", Secondary = "#FBBF24", Accent = "#8B5CF6", Background = "#0F172A", Text = "#F8FAFC", Border = "#334155", Notes = "Dark tech colors + trust + vibrant accents" },
            new ProductPalette { Name = "SocialMedia", Keywords = new KeywordList(new[] { "social", "media", "app" }), Primary = "#2563EB", Secondary = "#60A5FA", Accent = "#F43F5E", Background = "#F8FAFC", Text = "#1E293B", Border = "#DBEAFE", Notes = "Vibrant + engagement colors" },
            new ProductPalette { Name = "ProductivityTool", Keywords = new KeywordList(new[] { "productivity", "tool" }), Primary = "#059669", Secondary = "#34D399", Accent = "#2563EB", Background = "#F8FAFC", Text = "#1E293B", Border = "#D1FAE5", Notes = "Clear hierarchy + functional colors" },
            new ProductPalette { Name = "DesignSystem", Keywords = new KeywordList(new[] { "design", "system", "component", "library" }), Primary = "#7C3AED", Secondary = "#A78BFA", Accent = "#F472B6", Background = "#FAFAFA", Text = "#1F2937", Border = "#E5E7EB", Notes = "Clear hierarchy + code-like structure" },
            new ProductPalette { Name = "AIChatbot", Keywords = new KeywordList(new[] { "chatbot", "platform" }), Primary = "#7C3AED", Secondary = "#A78BFA", Accent = "#06B6D4", Background = "#FAF5FF", Text = "#1E1B4B", Border = "#DDD6FE", Notes = "Neutral + AI Purple (#6366F1)" },
            new ProductPalette { Name = "NFTWeb3", Keywords = new KeywordList(new[] { "nft", "web3", "platform" }), Primary = "#8B5CF6", Secondary = "#C4B5FD", Accent = "#FFD700", Background = "#0F0F23", Text = "#F8FAFC", Border = "#4C1D95", Notes = "Dark + Neon + Gold (#FFD700)" },
            new ProductPalette { Name = "CreatorEconomy", Keywords = new KeywordList(new[] { "creator", "economy", "platform" }), Primary = "#EC4899", Secondary = "#F9A8D4", Accent = "#8B5CF6", Background = "#FDF2F8", Text = "#831843", Border = "#FBCFE8", Notes = "Vibrant + Brand colors" },
            new ProductPalette { Name = "SustainabilityESG", Keywords = new KeywordList(new[] { "sustainability", "esg", "platform" }), Primary = "#228B22", Secondary = "#4ADE80", Accent = "#92400E", Background = "#F0FDF4", Text = "#14532D", Border = "#BBF7D0", Notes = "Green (#228B22) + Earth tones" },
            new ProductPalette { Name = "RemoteWork", Keywords = new KeywordList(new[] { "remote", "work", "collaboration", "tool" }), Primary = "#0EA5E9", Secondary = "#7DD3FC", Accent = "#F59E0B", Background = "#F0F9FF", Text = "#0C4A6E", Border = "#BAE6FD", Notes = "Calm Blue + Neutral grey" },
            new ProductPalette { Name = "MentalHealth", Keywords = new KeywordList(new[] { "mental", "health", "app" }), Primary = "#8B5CF6", Secondary = "#C4B5FD", Accent = "#14B8A6", Background = "#FAF5FF", Text = "#4C1D95", Border = "#DDD6FE", Notes = "Calm Pastels + Trust colors" },
            new ProductPalette { Name = "PetTech", Keywords = new KeywordList(new[] { "pet", "tech", "app" }), Primary = "#F97316", Secondary = "#FDBA74", Accent = "#84CC16", Background = "#FFF7ED", Text = "#9A3412", Border = "#FED7AA", Notes = "Playful + Warm colors" },
            new ProductPalette { Name = "SmartHomeIoT", Keywords = new KeywordList(new[] { "smart", "home", "iot", "dashboard" }), Primary = "#0EA5E9", Secondary = "#38BDF8", Accent = "#22C55E", Background = "#0F172A", Text = "#F1F5F9", Border = "#334155", Notes = "Dark + Status indicator colors" },
            new ProductPalette { Name = "EVCharging", Keywords = new KeywordList(new[] { "charging", "ecosystem" }), Primary = "#009CD1", Secondary = "#22D3EE", Accent = "#22C55E", Background = "#F0FDFA", Text = "#134E4A", Border = "#99F6E4", Notes = "Electric Blue (#009CD1) + Green" },
            new ProductPalette { Name = "SubscriptionBox", Keywords = new KeywordList(new[] { "subscription", "box", "service" }), Primary = "#E11D48", Secondary = "#FB7185", Accent = "#8B5CF6", Background = "#FFF1F2", Text = "#881337", Border = "#FECDD3", Notes = "Brand + Excitement colors" },
            new ProductPalette { Name = "Podcast", Keywords = new KeywordList(new[] { "podcast", "platform" }), Primary = "#7C3AED", Secondary = "#A78BFA", Accent = "#F472B6", Background = "#0F0F23", Text = "#F1F5F9", Border = "#4C1D95", Notes = "Dark + Audio waveform accents" },
            new ProductPalette { Name = "Dating", Keywords = new KeywordList(new[] { "dating", "app" }), Primary = "#E11D48", Secondary = "#FB7185", Accent = "#F472B6", Background = "#FFF1F2", Text = "#881337", Border = "#FECDD3", Notes = "Warm + Romantic (Pink/Red gradients)" },
            new ProductPalette { Name = "MicroCredentials", Keywords = new KeywordList(new[] { "micro", "credentials", "badges", "platform" }), Primary = "#2563EB", Secondary = "#60A5FA", Accent = "#FFD700", Background = "#EFF6FF", Text = "#1E40AF", Border = "#BFDBFE", Notes = "Trust Blue + Gold (#FFD700)" },
            new ProductPalette { Name = "KnowledgeBase", Keywords = new KeywordList(new[] { "knowledge", "base", "documentation" }), Primary = "#374151", Secondary = "#6B7280", Accent = "#3B82F6", Background = "#F9FAFB", Text = "#111827", Border = "#E5E7EB", Notes = "Clean hierarchy + minimal color" },
            new ProductPalette { Name = "HyperlocalServices", Keywords = new KeywordList(new[] { "hyperlocal", "services" }), Primary = "#DC2626", Secondary = "#F87171", Accent = "#2563EB", Background = "#FEF2F2", Text = "#991B1B", Border = "#FECACA", Notes = "Location markers + Trust colors" },
            new ProductPalette { Name = "BeautySpaWellness", Keywords = new KeywordList(new[] { "beauty", "spa", "wellness", "service" }), Primary = "#FFB6C1", Secondary = "#FECDD3", Accent = "#D4AF37", Background = "#FFF5F7", Text = "#831843", Border = "#FBCFE8", Notes = "Soft pastels (Pink #FFB6C1 Sage #90EE90) + Cream + Gold accents" },
            new ProductPalette { Name = "LuxuryPremium", Keywords = new KeywordList(new[] { "luxury", "premium", "brand" }), Primary = "#1C1917", Secondary = "#44403C", Accent = "#CA8A04", Background = "#FAFAF9", Text = "#0C0A09", Border = "#D6D3D1", Notes = "Black + Gold (#FFD700) + White + Minimal accent" },
            new ProductPalette { Name = "RestaurantFood", Keywords = new KeywordList(new[] { "restaurant", "food", "service" }), Primary = "#DC2626", Secondary = "#F87171", Accent = "#CA8A04", Background = "#FEF2F2", Text = "#450A0A", Border = "#FECACA", Notes = "Warm colors (Orange Red Brown) + appetizing imagery" },
            new ProductPalette { Name = "FitnessGym", Keywords = new KeywordList(new[] { "fitness", "gym", "app" }), Primary = "#FF6B35", Secondary = "#FB923C", Accent = "#22C55E", Background = "#18181B", Text = "#FAFAFA", Border = "#3F3F46", Notes = "Energetic (Orange #FF6B35 Electric Blue) + Dark bg" },
            new ProductPalette { Name = "RealEstate", Keywords = new KeywordList(new[] { "real", "estate", "property" }), Primary = "#0077B6", Secondary = "#0EA5E9", Accent = "#D4AF37", Background = "#F8FAFC", Text = "#0C4A6E", Border = "#BAE6FD", Notes = "Trust Blue (#0077B6) + Gold accents + White" },
            new ProductPalette { Name = "TravelTourism", Keywords = new KeywordList(new[] { "travel", "tourism", "agency" }), Primary = "#0EA5E9", Secondary = "#38BDF8", Accent = "#F97316", Background = "#F0F9FF", Text = "#0C4A6E", Border = "#BAE6FD", Notes = "Vibrant destination colors + Sky Blue + Warm accents" },
            new ProductPalette { Name = "HotelHospitality", Keywords = new KeywordList(new[] { "hotel", "hospitality" }), Primary = "#1E3A8A", Secondary = "#3B82F6", Accent = "#D4AF37", Background = "#F8FAFC", Text = "#1E40AF", Border = "#BFDBFE", Notes = "Warm neutrals + Gold (#D4AF37) + Brand accent" },
            new ProductPalette { Name = "WeddingEvent", Keywords = new KeywordList(new[] { "wedding", "event", "planning" }), Primary = "#FFD6E0", Secondary = "#FBCFE8", Accent = "#D4AF37", Background = "#FFF5F7", Text = "#831843", Border = "#F9A8D4", Notes = "Soft Pink (#FFD6E0) + Gold + Cream + Sage" },
            new ProductPalette { Name = "LegalServices", Keywords = new KeywordList(new[] { "legal", "services" }), Primary = "#1E3A5F", Secondary = "#1E40AF", Accent = "#D4AF37", Background = "#F8FAFC", Text = "#0F172A", Border = "#CBD5E1", Notes = "Navy Blue (#1E3A5F) + Gold + White" },
            new ProductPalette { Name = "Insurance", Keywords = new KeywordList(new[] { "insurance", "platform" }), Primary = "#0066CC", Secondary = "#3B82F6", Accent = "#16A34A", Background = "#F0F9FF", Text = "#1E3A5F", Border = "#BFDBFE", Notes = "Trust Blue (#0066CC) + Green (security) + Neutral" },
            new ProductPalette { Name = "BankingFinance", Keywords = new KeywordList(new[] { "banking", "traditional", "finance" }), Primary = "#0A1628", Secondary = "#1E3A8A", Accent = "#D4AF37", Background = "#F8FAFC", Text = "#0F172A", Border = "#CBD5E1", Notes = "Navy (#0A1628) + Trust Blue + Gold accents" },
            new ProductPalette { Name = "OnlineCourse", Keywords = new KeywordList(new[] { "online", "course", "learning" }), Primary = "#0D9488", Secondary = "#2DD4BF", Accent = "#EA580C", Background = "#F0FDFA", Text = "#134E4A", Border = "#5EEAD4", Notes = "Vibrant learning colors + Progress green" },
            new ProductPalette { Name = "NonProfit", Keywords = new KeywordList(new[] { "non", "profit", "charity" }), Primary = "#0891B2", Secondary = "#22D3EE", Accent = "#F97316", Background = "#ECFEFF", Text = "#164E63", Border = "#A5F3FC", Notes = "Cause-related colors + Trust + Warm" },
            new ProductPalette { Name = "MusicStreaming", Keywords = new KeywordList(new[] { "music", "streaming" }), Primary = "#1DB954", Secondary = "#1ED760", Accent = "#FF6B35", Background = "#121212", Text = "#FFFFFF", Border = "#282828", Notes = "Dark (#121212) + Vibrant accents + Album art colors" },
            new ProductPalette { Name = "VideoStreamingOTT", Keywords = new KeywordList(new[] { "video", "streaming", "ott" }), Primary = "#E50914", Secondary = "#B20710", Accent = "#FFFFFF", Background = "#141414", Text = "#FFFFFF", Border = "#333333", Notes = "Dark bg + Content poster colors + Brand accent" },
            new ProductPalette { Name = "JobBoard", Keywords = new KeywordList(new[] { "job", "board", "recruitment" }), Primary = "#0A66C2", Secondary = "#378FE9", Accent = "#16A34A", Background = "#F3F2EF", Text = "#000000", Border = "#E0DFDC", Notes = "Professional Blue + Success Green + Neutral" },
            new ProductPalette { Name = "MarketplaceP2P", Keywords = new KeywordList(new[] { "marketplace", "p2p" }), Primary = "#FF5A5F", Secondary = "#FF7E82", Accent = "#00A699", Background = "#F7F7F7", Text = "#484848", Border = "#EBEBEB", Notes = "Trust colors + Category colors + Success green" },
            new ProductPalette { Name = "LogisticsDelivery", Keywords = new KeywordList(new[] { "logistics", "delivery" }), Primary = "#2563EB", Secondary = "#3B82F6", Accent = "#F97316", Background = "#F8FAFC", Text = "#1E293B", Border = "#E2E8F0", Notes = "Blue (#2563EB) + Orange (tracking) + Green (delivered)" },
            new ProductPalette { Name = "AgricultureFarm", Keywords = new KeywordList(new[] { "agriculture", "farm", "tech" }), Primary = "#4A7C23", Secondary = "#65A30D", Accent = "#92400E", Background = "#FEFCE8", Text = "#365314", Border = "#D9F99D", Notes = "Earth Green (#4A7C23) + Brown + Sky Blue" },
            new ProductPalette { Name = "Construction", Keywords = new KeywordList(new[] { "construction", "architecture" }), Primary = "#4A4A4A", Secondary = "#6B7280", Accent = "#FF6B35", Background = "#F5F5F5", Text = "#1F2937", Border = "#D1D5DB", Notes = "Grey (#4A4A4A) + Orange (safety) + Blueprint Blue" },
            new ProductPalette { Name = "Automotive", Keywords = new KeywordList(new[] { "automotive", "car", "dealership" }), Primary = "#1C1C1C", Secondary = "#4B5563", Accent = "#EF4444", Background = "#F9FAFB", Text = "#111827", Border = "#D1D5DB", Notes = "Brand colors + Metallic accents + Dark/Light" },
            new ProductPalette { Name = "Photography", Keywords = new KeywordList(new[] { "photography", "studio" }), Primary = "#000000", Secondary = "#374151", Accent = "#FAFAFA", Background = "#FAFAFA", Text = "#000000", Border = "#E5E7EB", Notes = "Black + White + Minimal accent" },
            new ProductPalette { Name = "Coworking", Keywords = new KeywordList(new[] { "coworking", "space" }), Primary = "#F97316", Secondary = "#FDBA74", Accent = "#8B4513", Background = "#FFFBEB", Text = "#78350F", Border = "#FDE68A", Notes = "Energetic colors + Wood tones + Brand accent" },
            new ProductPalette { Name = "CleaningService", Keywords = new KeywordList(new[] { "cleaning", "service" }), Primary = "#00B4D8", Secondary = "#48CAE4", Accent = "#22C55E", Background = "#F0FDFA", Text = "#0C4A6E", Border = "#99F6E4", Notes = "Fresh Blue (#00B4D8) + Clean White + Green" },
            new ProductPalette { Name = "HomeServices", Keywords = new KeywordList(new[] { "home", "services", "plumber", "electrician" }), Primary = "#1E3A8A", Secondary = "#3B82F6", Accent = "#FF6B35", Background = "#F8FAFC", Text = "#1E293B", Border = "#E2E8F0", Notes = "Trust Blue + Safety Orange + Professional grey" },
            new ProductPalette { Name = "Childcare", Keywords = new KeywordList(new[] { "childcare", "daycare" }), Primary = "#F472B6", Secondary = "#FBCFE8", Accent = "#FCD34D", Background = "#FDF2F8", Text = "#831843", Border = "#F9A8D4", Notes = "Playful pastels + Safe colors + Warm accents" },
            new ProductPalette { Name = "SeniorCare", Keywords = new KeywordList(new[] { "senior", "care", "elderly" }), Primary = "#0891B2", Secondary = "#67E8F9", Accent = "#F59E0B", Background = "#ECFEFF", Text = "#164E63", Border = "#A5F3FC", Notes = "Calm Blue + Warm neutrals + Large text" },
            new ProductPalette { Name = "MedicalClinic", Keywords = new KeywordList(new[] { "medical", "clinic" }), Primary = "#0077B6", Secondary = "#48CAE4", Accent = "#16A34A", Background = "#F0F9FF", Text = "#0C4A6E", Border = "#BAE6FD", Notes = "Medical Blue (#0077B6) + Trust White + Calm Green" },
            new ProductPalette { Name = "Pharmacy", Keywords = new KeywordList(new[] { "pharmacy", "drug", "store" }), Primary = "#22C55E", Secondary = "#4ADE80", Accent = "#0077B6", Background = "#F0FDF4", Text = "#14532D", Border = "#BBF7D0", Notes = "Pharmacy Green + Trust Blue + Clean White" },
            new ProductPalette { Name = "DentalPractice", Keywords = new KeywordList(new[] { "dental", "practice" }), Primary = "#0EA5E9", Secondary = "#7DD3FC", Accent = "#FCD34D", Background = "#F0F9FF", Text = "#0C4A6E", Border = "#BAE6FD", Notes = "Fresh Blue + White + Smile Yellow accent" },
            new ProductPalette { Name = "VeterinaryClinic", Keywords = new KeywordList(new[] { "veterinary", "clinic" }), Primary = "#0891B2", Secondary = "#67E8F9", Accent = "#F97316", Background = "#ECFEFF", Text = "#164E63", Border = "#A5F3FC", Notes = "Caring Blue + Pet-friendly colors + Warm accents" },
            new ProductPalette { Name = "FloristPlant", Keywords = new KeywordList(new[] { "florist", "plant", "shop" }), Primary = "#22C55E", Secondary = "#86EFAC", Accent = "#EC4899", Background = "#F0FDF4", Text = "#14532D", Border = "#BBF7D0", Notes = "Natural Green + Floral pinks/purples + Earth tones" },
            new ProductPalette { Name = "BakeryCafe", Keywords = new KeywordList(new[] { "bakery", "cafe" }), Primary = "#92400E", Secondary = "#D97706", Accent = "#FCD34D", Background = "#FFFBEB", Text = "#78350F", Border = "#FDE68A", Notes = "Warm Brown + Cream + Appetizing accents" },
            new ProductPalette { Name = "CoffeeShop", Keywords = new KeywordList(new[] { "coffee", "shop" }), Primary = "#6F4E37", Secondary = "#A78B71", Accent = "#D4A574", Background = "#FEF7ED", Text = "#3D2914", Border = "#E8DDD4", Notes = "Coffee Brown (#6F4E37) + Cream + Warm accents" },
            new ProductPalette { Name = "BreweryWinery", Keywords = new KeywordList(new[] { "brewery", "winery" }), Primary = "#7C2D12", Secondary = "#C2410C", Accent = "#D4AF37", Background = "#FEF2F2", Text = "#450A0A", Border = "#FCA5A5", Notes = "Deep amber/burgundy + Gold + Craft aesthetic" },
            new ProductPalette { Name = "Airline", Keywords = new KeywordList(new[] { "airline" }), Primary = "#0EA5E9", Secondary = "#7DD3FC", Accent = "#F97316", Background = "#F0F9FF", Text = "#0C4A6E", Border = "#BAE6FD", Notes = "Sky Blue + Brand colors + Trust accents" },
            new ProductPalette { Name = "NewsMedia", Keywords = new KeywordList(new[] { "news", "media", "platform" }), Primary = "#18181B", Secondary = "#3F3F46", Accent = "#DC2626", Background = "#FAFAFA", Text = "#09090B", Border = "#E4E4E7", Notes = "Brand colors + High contrast + Category colors" },
            new ProductPalette { Name = "MagazineBlog", Keywords = new KeywordList(new[] { "magazine", "blog" }), Primary = "#EC4899", Secondary = "#F9A8D4", Accent = "#000000", Background = "#FFFFFF", Text = "#18181B", Border = "#F3F4F6", Notes = "Editorial colors + Brand primary + Clean white" },
            new ProductPalette { Name = "Freelancer", Keywords = new KeywordList(new[] { "freelancer", "platform" }), Primary = "#059669", Secondary = "#34D399", Accent = "#2563EB", Background = "#F8FAFC", Text = "#020617", Border = "#E2E8F0", Notes = "Professional Blue + Success Green + Neutral" },
            new ProductPalette { Name = "Consulting", Keywords = new KeywordList(new[] { "consulting", "firm" }), Primary = "#0F172A", Secondary = "#334155", Accent = "#D4AF37", Background = "#F8FAFC", Text = "#020617", Border = "#E2E8F0", Notes = "Navy + Gold + Professional grey" },
            new ProductPalette { Name = "MarketingAgency", Keywords = new KeywordList(new[] { "marketing", "agency" }), Primary = "#EC4899", Secondary = "#F472B6", Accent = "#06B6D4", Background = "#FDF2F8", Text = "#831843", Border = "#FBCFE8", Notes = "Bold brand colors + Creative freedom" },
            new ProductPalette { Name = "EventManagement", Keywords = new KeywordList(new[] { "event", "management" }), Primary = "#7C3AED", Secondary = "#A78BFA", Accent = "#F97316", Background = "#FAF5FF", Text = "#4C1D95", Border = "#DDD6FE", Notes = "Event theme colors + Excitement accents" },
            new ProductPalette { Name = "ConferenceWebinar", Keywords = new KeywordList(new[] { "conference", "webinar", "platform" }), Primary = "#0F172A", Secondary = "#334155", Accent = "#0369A1", Background = "#F8FAFC", Text = "#020617", Border = "#E2E8F0", Notes = "Professional Blue + Video accent + Brand" },
            new ProductPalette { Name = "MembershipCommunity", Keywords = new KeywordList(new[] { "membership", "community" }), Primary = "#7C3AED", Secondary = "#A78BFA", Accent = "#F97316", Background = "#FAF5FF", Text = "#4C1D95", Border = "#DDD6FE", Notes = "Community brand colors + Engagement accents" },
            new ProductPalette { Name = "Newsletter", Keywords = new KeywordList(new[] { "newsletter", "platform" }), Primary = "#F97316", Secondary = "#FB923C", Accent = "#1E293B", Background = "#FFFBEB", Text = "#1E293B", Border = "#FED7AA", Notes = "Brand primary + Clean white + CTA accent" },
            new ProductPalette { Name = "DigitalProducts", Keywords = new KeywordList(new[] { "digital", "products", "downloads" }), Primary = "#8B5CF6", Secondary = "#A78BFA", Accent = "#22C55E", Background = "#FAF5FF", Text = "#4C1D95", Border = "#DDD6FE", Notes = "Product category colors + Brand + Success green" },
            new ProductPalette { Name = "ChurchReligious", Keywords = new KeywordList(new[] { "church", "religious", "organization" }), Primary = "#4C1D95", Secondary = "#7C3AED", Accent = "#D4AF37", Background = "#FAF5FF", Text = "#1E1B4B", Border = "#DDD6FE", Notes = "Warm Gold + Deep Purple/Blue + White" },
            new ProductPalette { Name = "SportsTeam", Keywords = new KeywordList(new[] { "sports", "team", "club" }), Primary = "#DC2626", Secondary = "#F87171", Accent = "#1E3A8A", Background = "#FEF2F2", Text = "#7F1D1D", Border = "#FECACA", Notes = "Team colors + Energetic accents" },
            new ProductPalette { Name = "MuseumGallery", Keywords = new KeywordList(new[] { "museum", "gallery" }), Primary = "#374151", Secondary = "#6B7280", Accent = "#D4AF37", Background = "#FAFAFA", Text = "#111827", Border = "#E5E7EB", Notes = "Art-appropriate neutrals + Exhibition accents" },
            new ProductPalette { Name = "TheaterCinema", Keywords = new KeywordList(new[] { "theater", "cinema" }), Primary = "#991B1B", Secondary = "#DC2626", Accent = "#D4AF37", Background = "#18181B", Text = "#FAFAFA", Border = "#3F3F46", Notes = "Dark + Spotlight accents + Gold" },
            new ProductPalette { Name = "LanguageLearning", Keywords = new KeywordList(new[] { "language", "learning", "app" }), Primary = "#0D9488", Secondary = "#2DD4BF", Accent = "#EA580C", Background = "#F0FDFA", Text = "#134E4A", Border = "#5EEAD4", Notes = "Playful colors + Progress indicators + Country flags" },
            new ProductPalette { Name = "CodingBootcamp", Keywords = new KeywordList(new[] { "coding", "bootcamp" }), Primary = "#22C55E", Secondary = "#4ADE80", Accent = "#3B82F6", Background = "#0F172A", Text = "#F1F5F9", Border = "#334155", Notes = "Code editor colors + Brand + Success green" },
            new ProductPalette { Name = "Cybersecurity", Keywords = new KeywordList(new[] { "cybersecurity", "security", "cyber", "hacker" }), Primary = "#00FF41", Secondary = "#0D0D0D", Accent = "#00FF41", Background = "#000000", Text = "#E0E0E0", Border = "#1F1F1F", Notes = "Matrix Green + Deep Black + Terminal feel" },
            new ProductPalette { Name = "DeveloperTool", Keywords = new KeywordList(new[] { "developer", "tool", "ide", "code", "dev" }), Primary = "#3B82F6", Secondary = "#1E293B", Accent = "#2563EB", Background = "#0F172A", Text = "#F1F5F9", Border = "#334155", Notes = "Dark syntax theme colors + Blue focus" },
            new ProductPalette { Name = "Biotech", Keywords = new KeywordList(new[] { "biotech", "science", "biology", "medical" }), Primary = "#0EA5E9", Secondary = "#0284C7", Accent = "#10B981", Background = "#F8FAFC", Text = "#0F172A", Border = "#E2E8F0", Notes = "Sterile White + DNA Blue + Life Green" },
            new ProductPalette { Name = "SpaceTech", Keywords = new KeywordList(new[] { "space", "aerospace", "tech", "futuristic" }), Primary = "#FFFFFF", Secondary = "#94A3B8", Accent = "#3B82F6", Background = "#0B0B10", Text = "#F8FAFC", Border = "#1E293B", Notes = "Deep Space Black + Star White + Metallic" },
            new ProductPalette { Name = "ArchitectureInterior", Keywords = new KeywordList(new[] { "architecture", "interior", "design", "luxury" }), Primary = "#171717", Secondary = "#404040", Accent = "#D4AF37", Background = "#FFFFFF", Text = "#171717", Border = "#E5E5E5", Notes = "Monochrome + Gold Accent + High Imagery" },
            new ProductPalette { Name = "QuantumComputing", Keywords = new KeywordList(new[] { "quantum", "qubit", "tech" }), Primary = "#00FFFF", Secondary = "#7B61FF", Accent = "#FF00FF", Background = "#050510", Text = "#E0E0FF", Border = "#333344", Notes = "Interference patterns + Neon + Deep Dark" },
            new ProductPalette { Name = "Biohacking", Keywords = new KeywordList(new[] { "bio", "health", "science" }), Primary = "#FF4D4D", Secondary = "#4D94FF", Accent = "#00E676", Background = "#F5F5F7", Text = "#1C1C1E", Border = "#E5E5EA", Notes = "Biological red/blue + Clinical white" },
            new ProductPalette { Name = "AutonomousSystems", Keywords = new KeywordList(new[] { "drone", "robot", "fleet" }), Primary = "#00FF41", Secondary = "#008F11", Accent = "#FF3333", Background = "#0D1117", Text = "#E6EDF3", Border = "#30363D", Notes = "Terminal Green + Tactical Dark" },
            new ProductPalette { Name = "GenerativeAIArt", Keywords = new KeywordList(new[] { "art", "gen-ai", "creative" }), Primary = "#111111", Secondary = "#333333", Accent = "#FFFFFF", Background = "#FAFAFA", Text = "#000000", Border = "#E5E5E5", Notes = "Canvas Neutral + High Contrast" },
            new ProductPalette { Name = "SpatialVisionOS", Keywords = new KeywordList(new[] { "spatial", "glass", "vision" }), Primary = "#FFFFFF", Secondary = "#E5E5E5", Accent = "#007AFF", Background = "#888888", Text = "#000000", Border = "#FFFFFF", Notes = "Glass opacity 20% + System Blue" },
            new ProductPalette { Name = "ClimateTech", Keywords = new KeywordList(new[] { "climate", "green", "energy" }), Primary = "#2E8B57", Secondary = "#87CEEB", Accent = "#FFD700", Background = "#F0FFF4", Text = "#1A3320", Border = "#C6E6C6", Notes = "Nature Green + Solar Yellow + Air Blue" },
        ];

        /// <summary>
        /// Gets all available product palettes.
        /// </summary>
        public static IReadOnlyList<ProductPalette> GetAll() => ProductPalettes;

        /// <summary>
        /// Finds a product palette by name (case-insensitive).
        /// </summary>
        public static ProductPalette? FindByName(string name)
        {
            return ProductPalettes.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds product palettes matching any of the given keywords.
        /// </summary>
        public static List<ProductPalette> FindByKeywords(IEnumerable keywords)
        {
            var results = new List<ProductPalette>();
            if (keywords == null)
                return results;

            foreach (var palette in ProductPalettes)
            {
                foreach (var keyword in keywords)
                {
                    if (keyword is string text && palette.Keywords.Contains(text, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(palette);
                        break;
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Converts a ProductPalette to a ThemePalette for use with DaisyPaletteFactory.
        /// </summary>
        public static ThemePalette ToThemePalette(ProductPalette product)
        {
            var isDark = FloweryColorHelpers.IsDark(product.Background);

            return new ThemePalette
            {
                Primary = product.Primary,
                PrimaryFocus = FloweryColorHelpers.DarkenHex(product.Primary, 0.2),
                PrimaryContent = FloweryColorHelpers.GetContrastColorHex(product.Primary),

                Secondary = product.Secondary,
                SecondaryFocus = FloweryColorHelpers.DarkenHex(product.Secondary, 0.2),
                SecondaryContent = FloweryColorHelpers.GetContrastColorHex(product.Secondary),

                Accent = product.Accent,
                AccentFocus = FloweryColorHelpers.DarkenHex(product.Accent, 0.2),
                AccentContent = FloweryColorHelpers.GetContrastColorHex(product.Accent),

                Neutral = FloweryColorHelpers.DarkenHex(product.Text, 0.1),
                NeutralFocus = FloweryColorHelpers.DarkenHex(product.Text, 0.2),
                NeutralContent = FloweryColorHelpers.GetContrastColorHex(product.Text),

                Base100 = product.Background,
                Base200 = FloweryColorHelpers.AdjustBrightness(product.Background, isDark ? 0.05 : -0.05),
                Base300 = product.Border,
                BaseContent = product.Text,

                Info = isDark ? "#00BAFE" : "#2563EB",
                InfoContent = isDark ? "#000000" : "#FFFFFF",

                Success = isDark ? "#00D390" : "#16A34A",
                SuccessContent = isDark ? "#000000" : "#FFFFFF",

                Warning = isDark ? "#FCB700" : "#D97706",
                WarningContent = "#000000",

                Error = isDark ? "#FF627D" : "#DC2626",
                ErrorContent = isDark ? "#000000" : "#FFFFFF"
            };
        }

        /// <summary>
        /// Creates a ResourceDictionary from a ProductPalette.
        /// </summary>
        public static ResourceDictionary CreateResourceDictionary(ProductPalette product)
        {
            var themePalette = ToThemePalette(product);
            return DaisyPaletteFactory.Create(themePalette);
        }

        /// <summary>
        /// Creates a ResourceDictionary by product name.
        /// </summary>
        public static ResourceDictionary? CreateByName(string name)
        {
            var product = FindByName(name);
            return product != null ? CreateResourceDictionary(product) : null;
        }

        /// <summary>
        /// Generates C# source code for all product palettes pre-converted to ThemePalette format.
        /// Call this once and save the output to a .cs file to avoid runtime conversion overhead.
        /// </summary>
        /// <returns>A complete C# class file as a string.</returns>
        public static string GeneratePrecompiledPalettesCode()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("// Auto-generated file - DO NOT EDIT MANUALLY");
            sb.AppendLine("// Generated by ProductPaletteFactory.GeneratePrecompiledPalettesCode()");
            sb.AppendLine($"// Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Flowery.Theming");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Pre-compiled product theme palettes. No runtime conversion needed.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class ProductPalettes");
            sb.AppendLine("    {");
            sb.AppendLine("        private static readonly Dictionary<string, ThemePalette> Palettes = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");

            foreach (var product in ProductPalettes)
            {
                var tp = ToThemePalette(product);
                sb.AppendLine($"            [\"{product.Name}\"] = new ThemePalette");
                sb.AppendLine("            {");
                sb.AppendLine($"                Primary = \"{tp.Primary}\", PrimaryFocus = \"{tp.PrimaryFocus}\", PrimaryContent = \"{tp.PrimaryContent}\",");
                sb.AppendLine($"                Secondary = \"{tp.Secondary}\", SecondaryFocus = \"{tp.SecondaryFocus}\", SecondaryContent = \"{tp.SecondaryContent}\",");
                sb.AppendLine($"                Accent = \"{tp.Accent}\", AccentFocus = \"{tp.AccentFocus}\", AccentContent = \"{tp.AccentContent}\",");
                sb.AppendLine($"                Neutral = \"{tp.Neutral}\", NeutralFocus = \"{tp.NeutralFocus}\", NeutralContent = \"{tp.NeutralContent}\",");
                sb.AppendLine($"                Base100 = \"{tp.Base100}\", Base200 = \"{tp.Base200}\", Base300 = \"{tp.Base300}\", BaseContent = \"{tp.BaseContent}\",");
                sb.AppendLine($"                Info = \"{tp.Info}\", InfoContent = \"{tp.InfoContent}\", Success = \"{tp.Success}\", SuccessContent = \"{tp.SuccessContent}\",");
                sb.AppendLine($"                Warning = \"{tp.Warning}\", WarningContent = \"{tp.WarningContent}\", Error = \"{tp.Error}\", ErrorContent = \"{tp.ErrorContent}\"");
                sb.AppendLine("            },");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Gets all available product palette names.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static IEnumerable<string> GetAllNames() => Palettes.Keys;");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Gets a pre-compiled ThemePalette by name.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static ThemePalette? Get(string name)");
            sb.AppendLine("        {");
            sb.AppendLine("            return Palettes.TryGetValue(name, out var palette) ? palette : null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Checks if a product palette exists.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static bool Exists(string name) => Palettes.ContainsKey(name);");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Generates the precompiled palettes code and writes it to a file.
        /// </summary>
        /// <param name="outputPath">The file path to write to.</param>
        public static void GeneratePrecompiledPalettesFile(string outputPath)
        {
            var code = GeneratePrecompiledPalettesCode();
            System.IO.File.WriteAllText(outputPath, code);
        }
    }
}
