namespace Advertified.App.Configuration;

public sealed class LeadIndustryPolicyOptions
{
    public const string SectionName = "LeadIndustryPolicy";

    public List<LeadIndustryPolicyProfileOptions> Profiles { get; set; } = BuildDefaults();

    public static List<LeadIndustryPolicyProfileOptions> BuildDefaults()
    {
        return new List<LeadIndustryPolicyProfileOptions>
        {
            new()
            {
                Key = "funeral_services",
                Name = "Funeral Services",
                ObjectiveOverride = "leads",
                PreferredTone = "balanced",
                PreferredChannels = new List<string> { "Search", "Radio", "OOH" },
                Cta = "Speak to our team for guidance",
                MessagingAngle = "trust, dignity, and immediate local support",
                Guardrails = new List<string>
                {
                    "Avoid aggressive urgency or discount framing.",
                    "Use compassionate, respectful language.",
                    "Prioritize service trust over hard-sell tactics.",
                },
                AdditionalGap = "Opportunity to improve trust-led local discoverability for urgent family decisions.",
                AdditionalOutcome = "Expected impact: improved qualified enquiries and stronger community trust presence.",
                MatchKeywords = new List<string> { "funeral", "memorial", "burial" },
            },
            new()
            {
                Key = "healthcare",
                Name = "Healthcare",
                ObjectiveOverride = "leads",
                PreferredTone = "balanced",
                PreferredChannels = new List<string> { "Search", "OOH", "Radio" },
                Cta = "Book a consultation",
                MessagingAngle = "credibility, safety, and clear local access",
                Guardrails = new List<string>
                {
                    "Avoid absolute treatment claims.",
                    "Keep language clear and compliant.",
                    "Lead with trust and accessibility.",
                },
                AdditionalGap = "Opportunity to strengthen high-intent service capture for nearby patients.",
                AdditionalOutcome = "Expected impact: higher consultation intent and stronger local appointment flow.",
                MatchKeywords = new List<string> { "health", "clinic", "medical", "dental", "doctor", "hospital" },
            },
            new()
            {
                Key = "legal_services",
                Name = "Legal Services",
                ObjectiveOverride = "leads",
                PreferredTone = "performance",
                PreferredChannels = new List<string> { "Search", "Radio", "Digital" },
                Cta = "Request legal guidance",
                MessagingAngle = "authority, clarity, and response confidence",
                Guardrails = new List<string>
                {
                    "Avoid guaranteed case outcomes.",
                    "Use precise, professional language.",
                    "Focus on trust and next-step clarity.",
                },
                AdditionalGap = "Opportunity to capture urgent high-intent searches before competitor firms.",
                AdditionalOutcome = "Expected impact: improved lead quality and stronger inbound case enquiries.",
                MatchKeywords = new List<string> { "legal", "attorney", "law", "advocate" },
            },
            new()
            {
                Key = "retail",
                Name = "Retail",
                ObjectiveOverride = "promotion",
                PreferredTone = "performance",
                PreferredChannels = new List<string> { "OOH", "Radio", "Digital" },
                Cta = "Visit us today",
                MessagingAngle = "local visibility and repeat customer flow",
                Guardrails = new List<string>
                {
                    "Balance promo with brand consistency.",
                    "Avoid over-reliance on discount-only messaging.",
                    "Keep campaign continuity between promo windows.",
                },
                AdditionalGap = "Opportunity to convert promotional momentum into always-on visibility.",
                AdditionalOutcome = "Expected impact: steadier footfall and stronger repeat demand beyond promotions.",
                MatchKeywords = new List<string> { "retail", "shop", "store", "grocery", "supermarket", "furniture" },
            },
            new()
            {
                Key = "default",
                Name = "General Services",
                PreferredTone = "balanced",
                PreferredChannels = new List<string> { "Digital", "OOH" },
                Cta = "Contact us to get started",
                MessagingAngle = "visible, credible, and easy to act on",
                Guardrails = new List<string>
                {
                    "Keep claims realistic and evidence-based.",
                    "Use clear, practical CTA language.",
                    "Align messaging with local demand intent.",
                },
                AdditionalGap = "Opportunity to tighten channel mix around the strongest local demand signals.",
                AdditionalOutcome = "Expected impact: clearer positioning and better conversion from existing demand.",
                MatchKeywords = new List<string>(),
            },
        };
    }
}

public sealed class LeadIndustryPolicyProfileOptions
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ObjectiveOverride { get; set; }

    public string? PreferredTone { get; set; }

    public List<string> PreferredChannels { get; set; } = new();

    public string Cta { get; set; } = string.Empty;

    public string MessagingAngle { get; set; } = string.Empty;

    public List<string> Guardrails { get; set; } = new();

    public string AdditionalGap { get; set; } = string.Empty;

    public string AdditionalOutcome { get; set; } = string.Empty;

    public List<string> MatchKeywords { get; set; } = new();
}
