const fs = require("fs");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..");
const seedPath = path.join(repoRoot, "exports", "social_inventory_seed.csv");
const outputRoot = path.join(repoRoot, "exports");
const digitalOutputRoot = path.join(outputRoot, "digital_inventory_intelligence");

const columns = [
  "platform_name",
  "row_kind",
  "package_name",
  "package_type",
  "primary_objective",
  "secondary_objective",
  "billing_model",
  "billing_event",
  "coverage_type",
  "target_audience",
  "existing_premium_mass_fit",
  "existing_environment_type",
  "existing_data_confidence",
  "platform_tier",
  "platform_family",
  "audience_income_fit",
  "premium_mass_fit",
  "price_positioning_fit",
  "youth_fit",
  "family_fit",
  "professional_fit",
  "high_value_client_fit",
  "b2b_fit",
  "lead_generation_fit",
  "awareness_fit",
  "conversion_fit",
  "remarketing_fit",
  "video_fit",
  "static_fit",
  "short_form_fit",
  "intent_strength",
  "demand_capture_fit",
  "interest_discovery_fit",
  "language_context_fit",
  "audience_age_skew",
  "audience_gender_skew",
  "content_environment",
  "buying_behaviour_fit",
  "brand_safety_fit",
  "objective_fit_primary",
  "objective_fit_secondary",
  "primary_audience_tags",
  "secondary_audience_tags",
  "recommendation_tags",
  "intelligence_notes",
  "source_urls",
  "data_confidence",
  "updated_by",
  "internal_key",
  "media_outlet_code",
  "source_type"
];

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

function parseCsvLine(line) {
  const values = [];
  let current = "";
  let inQuotes = false;

  for (let index = 0; index < line.length; index += 1) {
    const char = line[index];
    if (char === '"') {
      if (inQuotes && line[index + 1] === '"') {
        current += '"';
        index += 1;
      } else {
        inQuotes = !inQuotes;
      }
      continue;
    }

    if (char === "," && !inQuotes) {
      values.push(current);
      current = "";
      continue;
    }

    current += char;
  }

  values.push(current);
  return values;
}

function parseCsv(text) {
  const lines = text.split(/\r?\n/).filter(Boolean);
  if (lines.length === 0) {
    return [];
  }

  const headers = parseCsvLine(lines[0]);
  return lines.slice(1).map((line) => {
    const values = parseCsvLine(line);
    const row = {};
    headers.forEach((header, index) => {
      row[header] = values[index] || "";
    });
    return row;
  });
}

function toCsvValue(value) {
  const stringValue = value == null ? "" : String(value);
  if (/[",\r\n]/.test(stringValue)) {
    return `"${stringValue.replace(/"/g, '""')}"`;
  }

  return stringValue;
}

function writeCsv(filePath, rows) {
  const lines = [columns.join(",")];
  for (const row of rows) {
    lines.push(columns.map((column) => toCsvValue(row[column])).join(","));
  }

  fs.writeFileSync(filePath, `${lines.join("\r\n")}\r\n`, "utf8");
}

function sanitizeFileName(value) {
  return value
    .toLowerCase()
    .replace(/&/g, "and")
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .slice(0, 80);
}

function buildRow(seedRow) {
  const mediaOutletCode = `social_${seedRow.platform_code}`;
  const packageName = seedRow.package_name || "";
  return {
    platform_name: seedRow.platform_name || "",
    row_kind: "package",
    package_name: packageName,
    package_type: seedRow.package_type || "",
    primary_objective: seedRow.primary_objective || "",
    secondary_objective: seedRow.secondary_objective || "",
    billing_model: seedRow.billing_model || "",
    billing_event: seedRow.billing_event || "",
    coverage_type: seedRow.coverage_type || "",
    target_audience: seedRow.target_audience || "",
    existing_premium_mass_fit: seedRow.premium_mass_fit || "",
    existing_environment_type: seedRow.strategy_environment_type || "",
    existing_data_confidence: seedRow.data_confidence || "",
    platform_tier: "",
    platform_family: "",
    audience_income_fit: "",
    premium_mass_fit: "",
    price_positioning_fit: "",
    youth_fit: "",
    family_fit: "",
    professional_fit: "",
    high_value_client_fit: "",
    b2b_fit: "",
    lead_generation_fit: "",
    awareness_fit: "",
    conversion_fit: "",
    remarketing_fit: "",
    video_fit: "",
    static_fit: "",
    short_form_fit: "",
    intent_strength: "",
    demand_capture_fit: "",
    interest_discovery_fit: "",
    language_context_fit: "",
    audience_age_skew: "",
    audience_gender_skew: "",
    content_environment: "",
    buying_behaviour_fit: "",
    brand_safety_fit: "",
    objective_fit_primary: "",
    objective_fit_secondary: "",
    primary_audience_tags: (seedRow.keyword_seed || "").replaceAll(",", ";"),
    secondary_audience_tags: "",
    recommendation_tags: "",
    intelligence_notes: "",
    source_urls: (seedRow.source_urls || "").replaceAll(" | ", "; "),
    data_confidence: "",
    updated_by: "",
    internal_key: `${mediaOutletCode}|package|${packageName}`,
    media_outlet_code: mediaOutletCode,
    source_type: "digital_package"
  };
}

ensureDir(digitalOutputRoot);
const rows = parseCsv(fs.readFileSync(seedPath, "utf8"));
const grouped = new Map();

for (const row of rows) {
  const key = row.platform_name || row.platform_code;
  const existing = grouped.get(key) || [];
  existing.push(buildRow(row));
  grouped.set(key, existing);
}

const manifestRows = [];
for (const [platformName, platformRows] of grouped.entries()) {
  const fileName = `${sanitizeFileName(platformName)}.csv`;
  const relativePath = path.join("exports", "digital_inventory_intelligence", fileName);
  writeCsv(path.join(digitalOutputRoot, fileName), platformRows);
  manifestRows.push({
    media_type: "digital",
    station_or_channel_name: platformName,
    media_outlet_code: platformRows[0]?.media_outlet_code || "",
    row_count: platformRows.length,
    relative_path: relativePath
  });
}

const manifestColumns = ["media_type", "station_or_channel_name", "media_outlet_code", "row_count", "relative_path"];
const manifestLines = [manifestColumns.join(",")];
for (const row of manifestRows) {
  manifestLines.push(manifestColumns.map((column) => toCsvValue(row[column])).join(","));
}

fs.writeFileSync(path.join(outputRoot, "digital_inventory_intelligence_manifest.csv"), `${manifestLines.join("\r\n")}\r\n`, "utf8");
console.log(`Generated ${manifestRows.length} digital CSVs.`);
