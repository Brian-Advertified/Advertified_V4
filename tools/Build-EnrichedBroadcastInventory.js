const fs = require("fs");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..");
const inventoryPath = path.join(repoRoot, "src", "Advertified.App", "App_Data", "broadcast", "enriched_broadcast_inventory_normalized.json");
const newspaperCsvPath = process.argv[2] || path.join(repoRoot, "src", "Advertified.App", "App_Data", "inventory", "newspaper_media_planner_2026.csv");
const radioCsvPath = process.argv[3] || path.join(repoRoot, "src", "Advertified.App", "App_Data", "inventory", "radio_final_enriched.csv");

const defaultPsccmArea = 80;
const defaultThousands = 10;
const defaultCpmThousands = 100;

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8").replace(/^\uFEFF/, ""));
}

function parseCsv(text) {
  const rows = [];
  let row = [];
  let field = "";
  let inQuotes = false;

  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    const next = text[i + 1];

    if (inQuotes && char === "\"" && next === "\"") {
      field += "\"";
      i++;
      continue;
    }

    if (char === "\"") {
      inQuotes = !inQuotes;
      continue;
    }

    if (!inQuotes && char === ",") {
      row.push(field);
      field = "";
      continue;
    }

    if (!inQuotes && (char === "\n" || char === "\r")) {
      if (char === "\r" && next === "\n") {
        i++;
      }
      row.push(field);
      if (row.some((value) => value.length > 0)) {
        rows.push(row);
      }
      row = [];
      field = "";
      continue;
    }

    field += char;
  }

  row.push(field);
  if (row.some((value) => value.length > 0)) {
    rows.push(row);
  }

  if (rows.length === 0) {
    return [];
  }

  const headers = rows[0].map((header) => header.trim());
  return rows.slice(1).map((values) => {
    const output = {};
    headers.forEach((header, index) => {
      output[header] = (values[index] || "").trim();
    });
    return output;
  });
}

function normalizeToken(value) {
  return (value || "").replace(/[^a-z0-9]+/gi, "_").replace(/^_+|_+$/g, "").toLowerCase();
}

function toNumber(value) {
  const parsed = Number(String(value || "").replace(/[^0-9.-]/g, ""));
  return Number.isFinite(parsed) ? parsed : null;
}

function parseList(value) {
  return String(value || "")
    .split(/[;,]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function firstNonEmpty(...values) {
  return values.find((value) => typeof value === "string" && value.trim().length > 0)?.trim() || null;
}

function sanitizeText(value) {
  if (typeof value !== "string") {
    return value;
  }

  return value
    .replace(/\bmusic content during nan:00 hour\.\s*/gi, "")
    .replace(/\s+/g, " ")
    .trim();
}

function sanitizeRecord(value) {
  if (Array.isArray(value)) {
    return value.map(sanitizeRecord);
  }

  if (value && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, sanitizeRecord(item)]));
  }

  return sanitizeText(value);
}

function mergeRadioStationIntelligence(records, radioRows) {
  const stationRows = radioRows.filter((row) => String(row.row_kind || "").toLowerCase() === "station");
  const byCode = new Map(stationRows.map((row) => [String(row.media_outlet_code || "").toLowerCase(), row]));
  const byName = new Map(stationRows.map((row) => [String(row.station_name || "").toLowerCase(), row]));

  return records.map((record) => {
    if (String(record.media_type || "").toLowerCase() !== "radio") {
      return record;
    }

    const row = byCode.get(String(record.id || "").toLowerCase())
      || byName.get(String(record.station || "").toLowerCase());
    if (!row) {
      return {
        ...record,
        target_audience: firstNonEmpty(record.target_audience, `${record.station} radio audience across ${record.coverage_type || "South Africa"}.`)
      };
    }

    const provinceCodes = parseList(row.province_codes);
    const cityLabels = parseList(row.city_labels);
    const languageCodes = parseList(row.language_codes);
    const audienceKeywords = parseList(row.primary_audience_tags);

    return {
      ...record,
      broadcast_frequency: firstNonEmpty(row.broadcast_frequency, record.broadcast_frequency),
      coverage_type: firstNonEmpty(row.coverage_type, record.coverage_type) || record.coverage_type,
      province_codes: provinceCodes.length > 0 ? provinceCodes : record.province_codes,
      city_labels: cityLabels.length > 0 ? cityLabels : record.city_labels,
      primary_languages: languageCodes.length > 0 ? languageCodes : record.primary_languages,
      language_display: firstNonEmpty(record.language_display, row.primary_language, row.language_codes),
      language_notes: firstNonEmpty(record.language_notes, row.language_codes),
      audience_age_skew: firstNonEmpty(row.audience_age_skew, row.existing_audience_age_skew, record.audience_age_skew),
      audience_gender_skew: firstNonEmpty(row.audience_gender_skew, row.existing_audience_gender_skew, record.audience_gender_skew),
      audience_lsm_range: firstNonEmpty(row.existing_audience_lsm_range, record.audience_lsm_range),
      target_audience: firstNonEmpty(row.target_audience, record.target_audience, `${record.station} radio audience across ${record.coverage_type || "South Africa"}.`),
      audience_keywords: audienceKeywords.length > 0 ? audienceKeywords : record.audience_keywords,
      buying_behaviour_fit: firstNonEmpty(row.buying_behaviour_fit, record.buying_behaviour_fit),
      price_positioning_fit: firstNonEmpty(row.price_positioning_fit, record.price_positioning_fit),
      objective_fit_primary: firstNonEmpty(row.objective_fit_primary, record.objective_fit_primary),
      objective_fit_secondary: firstNonEmpty(row.objective_fit_secondary, record.objective_fit_secondary),
      environment_type: firstNonEmpty(row.content_environment, record.environment_type),
      premium_mass_fit: firstNonEmpty(row.premium_mass_fit, record.premium_mass_fit),
      data_confidence: firstNonEmpty(row.data_confidence, record.data_confidence, "medium"),
      intelligence_notes: firstNonEmpty(row.intelligence_notes, record.intelligence_notes)
    };
  });
}

function estimatePlanningRate(row) {
  const baseRate = toNumber(row["Base Rate (R excl VAT)"]);
  if (!baseRate || baseRate <= 0) {
    return null;
  }

  const unit = (row["Billing Unit"] || "").trim().toLowerCase();
  if (unit === "psccm") {
    return Math.round(baseRate * defaultPsccmArea * 100) / 100;
  }

  if (unit === "per thousand") {
    return Math.round(baseRate * defaultThousands * 100) / 100;
  }

  if (unit === "cpm") {
    return Math.round(baseRate * defaultCpmThousands * 100) / 100;
  }

  return baseRate;
}

function resolveRegion(regionRun) {
  const normalized = normalizeToken(regionRun);
  const provinceMap = {
    eastern_cape: "eastern_cape",
    gauteng: "gauteng",
    kwa_zulu_natal: "kwazulu_natal",
    kwazulu_natal: "kwazulu_natal",
    western_cape: "western_cape",
    free_state: "free_state",
    limpopo: "limpopo",
    mpumalanga: "mpumalanga",
    northern_cape: "northern_cape",
    north_west: "north_west"
  };

  if (!normalized || normalized === "national" || normalized === "digital") {
    return { coverageType: "national", provinceCodes: ["national"], cityLabels: ["National"], isNational: true };
  }

  return {
    coverageType: "provincial",
    provinceCodes: [provinceMap[normalized] || normalized],
    cityLabels: [],
    isNational: false
  };
}

function buildPackage(row) {
  const investment = estimatePlanningRate(row);
  if (!investment) {
    return null;
  }

  const nameParts = [
    row["Category"],
    row["Ad Product"],
    row["Colour/Type"],
    row["Specs / Size"],
    row["Region/Run"]
  ].filter(Boolean);

  const notes = [
    `Inventory ID: ${row["Inventory ID"]}.`,
    `Publisher: ${row["Publisher"] || "Unknown"}.`,
    `Billing unit: ${row["Billing Unit"] || "rate card"}. Base rate: R${row["Base Rate (R excl VAT)"] || investment} excl VAT.`,
    `Planning investment estimate: R${investment} excl VAT.`,
    row["Booking Deadline"] ? `Booking deadline: ${row["Booking Deadline"]}.` : "",
    row["Material Deadline"] ? `Material deadline: ${row["Material Deadline"]}.` : "",
    row["Contact"] ? `Contact: ${row["Contact"]}.` : "",
    row["Source"] ? `Source: ${row["Source"]}.` : ""
  ].filter(Boolean).join(" ");

  return {
    name: nameParts.join(" - ") || row["Ad Product"] || row["Inventory ID"],
    package_type: normalizeToken(row["Category"] || row["Ad Product"] || "newspaper"),
    investment_zar: investment,
    duration_months: 1,
    notes
  };
}

function buildNewspaperRecords(rows) {
  const groups = new Map();
  for (const row of rows) {
    const publication = row["Publication"];
    if (!publication) {
      continue;
    }

    if (!groups.has(publication.toLowerCase())) {
      groups.set(publication.toLowerCase(), { publication, rows: [] });
    }

    groups.get(publication.toLowerCase()).rows.push(row);
  }

  return Array.from(groups.values()).map((group) => {
    const sample = group.rows[0];
    const region = resolveRegion(sample["Region/Run"]);
    const packages = group.rows.map(buildPackage).filter(Boolean);
    const audienceKeywords = [
      group.publication,
      sample["Publisher"],
      sample["Region/Run"],
      "newspaper",
      "print",
      "news"
    ].filter(Boolean);

    return {
      id: `newspaper_${normalizeToken(group.publication)}`,
      station: group.publication,
      media_type: "newspaper",
      catalog_health: "strong",
      coverage_type: region.coverageType,
      broadcast_frequency: "print and digital rate card",
      province_codes: region.provinceCodes,
      city_labels: region.cityLabels,
      primary_languages: ["english"],
      language_display: "English",
      language_notes: "English",
      listenership_daily: null,
      listenership_weekly: null,
      listenership_period: "Newspaper planner rate card import",
      audience_age_skew: null,
      audience_gender_skew: null,
      audience_lsm_range: null,
      audience_racial_skew: null,
      urban_rural_mix: region.coverageType === "national" ? "national readership" : "regional readership",
      target_audience: `${group.publication} newspaper readership across ${sample["Region/Run"] || "South Africa"}.`,
      audience_keywords: audienceKeywords,
      buying_behaviour_fit: "planned_purchase",
      price_positioning_fit: "mid_to_high",
      sales_model_fit: "lead_generation",
      objective_fit_primary: "awareness",
      objective_fit_secondary: "consideration",
      environment_type: "newspaper",
      premium_mass_fit: region.coverageType === "national" ? "premium" : "regional",
      data_confidence: "medium",
      intelligence_notes: `Imported from Advertified newspaper media planner 2026. Default planning estimates: psccm uses ${defaultPsccmArea} psccm, per-thousand uses ${defaultThousands}k inserts, CPM uses ${defaultCpmThousands}k impressions.`,
      packages,
      pricing: [],
      data_source_enrichment: group.rows
        .map((row) => row["Source"])
        .filter(Boolean)
        .filter((source, index, values) => values.indexOf(source) === index)
        .map((source) => ({ source_name: "newspaper_media_planner_2026.csv", source_url: source })),
      has_pricing: packages.length > 0,
      is_national: region.isNational
    };
  });
}

if (!fs.existsSync(newspaperCsvPath)) {
  throw new Error(`Newspaper CSV was not found: ${newspaperCsvPath}`);
}

if (!fs.existsSync(radioCsvPath)) {
  throw new Error(`Radio intelligence CSV was not found: ${radioCsvPath}`);
}

const inventory = readJson(inventoryPath);
const csvRows = parseCsv(fs.readFileSync(newspaperCsvPath, "utf8").replace(/^\uFEFF/, ""));
const radioRows = parseCsv(fs.readFileSync(radioCsvPath, "utf8").replace(/^\uFEFF/, ""));
const newspaperRecords = buildNewspaperRecords(csvRows);
const nonNewspaperRecords = mergeRadioStationIntelligence(inventory.records || [], radioRows).filter((record) => {
  const mediaType = String(record.media_type || "").toLowerCase();
  return mediaType !== "newspaper" && mediaType !== "print";
}).map(sanitizeRecord);

inventory.schema_version = "2.2.0";
inventory.generated_at = new Date().toISOString();
inventory.generated_from = [
  "advertified-tv-radio-planner-data-with-targets-populated.json",
  "radio_final_enriched.csv",
  "newspaper_media_planner_2026.csv"
];
inventory.engine_guidance = {
  ...(inventory.engine_guidance || {}),
  newspaper_note: "Newspaper rows are represented as package records in the existing broadcast inventory import path."
};
inventory.records = [...nonNewspaperRecords, ...newspaperRecords];

fs.writeFileSync(inventoryPath, `${JSON.stringify(inventory, null, 2)}\n`, "utf8");
console.log(`Wrote ${inventory.records.length} inventory records including ${newspaperRecords.length} newspaper publications from ${csvRows.length} newspaper rows and ${radioRows.length} radio intelligence rows.`);
