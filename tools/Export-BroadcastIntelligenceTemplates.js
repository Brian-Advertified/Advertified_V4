const fs = require("fs");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..");
const inventoryPath = path.join(
  repoRoot,
  "src",
  "Advertified.App",
  "App_Data",
  "broadcast",
  "enriched_broadcast_inventory_normalized.json"
);

const outputRoot = path.join(repoRoot, "exports");
const radioOutputRoot = path.join(outputRoot, "radio_inventory_intelligence");
const tvOutputRoot = path.join(outputRoot, "tv_inventory_intelligence");

const radioColumns = [
  "station_name",
  "row_kind",
  "slot",
  "day_group",
  "package_name",
  "broadcast_frequency",
  "coverage_type",
  "province_codes",
  "city_labels",
  "language_codes",
  "target_audience",
  "existing_audience_age_skew",
  "existing_audience_gender_skew",
  "existing_audience_lsm_range",
  "station_tier",
  "station_format",
  "audience_income_fit",
  "premium_mass_fit",
  "price_positioning_fit",
  "youth_fit",
  "family_fit",
  "professional_fit",
  "commuter_fit",
  "high_value_client_fit",
  "business_decision_maker_fit",
  "morning_drive_fit",
  "workday_fit",
  "afternoon_drive_fit",
  "evening_fit",
  "weekend_fit",
  "urban_rural_fit",
  "language_context_fit",
  "buying_behaviour_fit",
  "brand_safety_fit",
  "objective_fit_primary",
  "objective_fit_secondary",
  "audience_age_skew",
  "audience_gender_skew",
  "content_environment",
  "presenter_or_show_context",
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

const tvColumns = [
  "channel_name",
  "row_kind",
  "programme_name",
  "slot",
  "day_group",
  "package_name",
  "broadcast_frequency",
  "coverage_type",
  "province_codes",
  "city_labels",
  "language_codes",
  "target_audience",
  "existing_audience_age_skew",
  "existing_audience_gender_skew",
  "existing_audience_lsm_range",
  "channel_tier",
  "channel_format",
  "genre_fit",
  "audience_income_fit",
  "premium_mass_fit",
  "price_positioning_fit",
  "youth_fit",
  "family_fit",
  "professional_fit",
  "household_decision_maker_fit",
  "high_value_client_fit",
  "news_affairs_fit",
  "sport_fit",
  "entertainment_fit",
  "appointment_viewing_fit",
  "co_viewing_fit",
  "language_context_fit",
  "buying_behaviour_fit",
  "brand_safety_fit",
  "objective_fit_primary",
  "objective_fit_secondary",
  "audience_age_skew",
  "audience_gender_skew",
  "content_environment",
  "programme_context",
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

function sanitizeFileName(value) {
  return value
    .toLowerCase()
    .replace(/&/g, "and")
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .slice(0, 80);
}

function toCsvValue(value) {
  const stringValue = value == null ? "" : String(value);
  if (/[",\r\n]/.test(stringValue)) {
    return `"${stringValue.replace(/"/g, '""')}"`;
  }

  return stringValue;
}

function writeCsv(filePath, columns, rows) {
  const lines = [columns.join(",")];
  for (const row of rows) {
    lines.push(columns.map((column) => toCsvValue(row[column])).join(","));
  }

  fs.writeFileSync(filePath, `${lines.join("\r\n")}\r\n`, "utf8");
}

function asTagString(items) {
  if (!Array.isArray(items) || items.length === 0) {
    return "";
  }

  return items.join("; ");
}

function asSourceUrlString(items) {
  if (!Array.isArray(items) || items.length === 0) {
    return "";
  }

  return items
    .map((item) => item && item.source_url)
    .filter(Boolean)
    .join("; ");
}

function createSharedRow(record, rowKind) {
  return {
    row_kind: rowKind,
    broadcast_frequency: record.broadcast_frequency || "",
    coverage_type: record.coverage_type || "",
    province_codes: (record.province_codes || []).join("; "),
    city_labels: (record.city_labels || []).join("; "),
    language_codes: (record.primary_languages || []).join("; "),
    target_audience: record.target_audience || "",
    existing_audience_age_skew: record.audience_age_skew || "",
    existing_audience_gender_skew: record.audience_gender_skew || "",
    existing_audience_lsm_range: record.audience_lsm_range || "",
    source_urls: asSourceUrlString(record.data_source_enrichment),
    data_confidence: "",
    updated_by: "",
    media_outlet_code: record.id || "",
    source_type: ""
  };
}

function createRadioRows(record) {
  const rows = [];

  rows.push({
    ...createSharedRow(record, "station"),
    station_name: record.station,
    slot: "",
    day_group: "",
    package_name: "",
    station_tier: "",
    station_format: "",
    audience_income_fit: "",
    premium_mass_fit: "",
    price_positioning_fit: "",
    youth_fit: "",
    family_fit: "",
    professional_fit: "",
    commuter_fit: "",
    high_value_client_fit: "",
    business_decision_maker_fit: "",
    morning_drive_fit: "",
    workday_fit: "",
    afternoon_drive_fit: "",
    evening_fit: "",
    weekend_fit: "",
    urban_rural_fit: "",
    language_context_fit: "",
    buying_behaviour_fit: "",
    brand_safety_fit: "",
    objective_fit_primary: "",
    objective_fit_secondary: "",
    audience_age_skew: "",
    audience_gender_skew: "",
    content_environment: "",
    presenter_or_show_context: "",
    primary_audience_tags: asTagString(record.audience_keywords),
    secondary_audience_tags: "",
    recommendation_tags: "",
    intelligence_notes: record.intelligence_notes || "",
    internal_key: `${record.id}|station`,
    source_type: "radio_station"
  });

  const pricing = record.pricing || {};
  if (Array.isArray(pricing)) {
    for (const entry of pricing) {
      const slot = entry.slot || entry.time || "";
      const dayGroup = entry.group || entry.day_group || "schedule";
      rows.push({
        ...createSharedRow(record, "slot"),
        station_name: record.station,
        slot,
        day_group: dayGroup,
        package_name: "",
        station_tier: "",
        station_format: "",
        audience_income_fit: "",
        premium_mass_fit: "",
        price_positioning_fit: "",
        youth_fit: "",
        family_fit: "",
        professional_fit: "",
        commuter_fit: "",
        high_value_client_fit: "",
        business_decision_maker_fit: "",
        morning_drive_fit: "",
        workday_fit: "",
        afternoon_drive_fit: "",
        evening_fit: "",
        weekend_fit: "",
        urban_rural_fit: "",
        language_context_fit: "",
        buying_behaviour_fit: "",
        brand_safety_fit: "",
        objective_fit_primary: "",
        objective_fit_secondary: "",
        audience_age_skew: "",
        audience_gender_skew: "",
        content_environment: "",
        presenter_or_show_context: entry.program || entry.programme || "",
        primary_audience_tags: asTagString(record.audience_keywords),
        secondary_audience_tags: "",
        recommendation_tags: "",
        intelligence_notes: "",
        internal_key: `${record.id}|slot|${dayGroup}|${slot}`,
        source_type: "radio_slot"
      });
    }
  } else {
    for (const [dayGroup, slots] of Object.entries(pricing)) {
      for (const slot of Object.keys(slots || {})) {
        rows.push({
          ...createSharedRow(record, "slot"),
          station_name: record.station,
          slot,
          day_group: dayGroup,
          package_name: "",
          station_tier: "",
          station_format: "",
          audience_income_fit: "",
          premium_mass_fit: "",
          price_positioning_fit: "",
          youth_fit: "",
          family_fit: "",
          professional_fit: "",
          commuter_fit: "",
          high_value_client_fit: "",
          business_decision_maker_fit: "",
          morning_drive_fit: "",
          workday_fit: "",
          afternoon_drive_fit: "",
          evening_fit: "",
          weekend_fit: "",
          urban_rural_fit: "",
          language_context_fit: "",
          buying_behaviour_fit: "",
          brand_safety_fit: "",
          objective_fit_primary: "",
          objective_fit_secondary: "",
          audience_age_skew: "",
          audience_gender_skew: "",
          content_environment: "",
          presenter_or_show_context: "",
          primary_audience_tags: asTagString(record.audience_keywords),
          secondary_audience_tags: "",
          recommendation_tags: "",
          intelligence_notes: "",
          internal_key: `${record.id}|slot|${dayGroup}|${slot}`,
          source_type: "radio_slot"
        });
      }
    }
  }

  for (const pkg of record.packages || []) {
    rows.push({
      ...createSharedRow(record, "package"),
      station_name: record.station,
      slot: "",
      day_group: "",
      package_name: pkg.name || "",
      station_tier: "",
      station_format: "",
      audience_income_fit: "",
      premium_mass_fit: "",
      price_positioning_fit: "",
      youth_fit: "",
      family_fit: "",
      professional_fit: "",
      commuter_fit: "",
      high_value_client_fit: "",
      business_decision_maker_fit: "",
      morning_drive_fit: "",
      workday_fit: "",
      afternoon_drive_fit: "",
      evening_fit: "",
      weekend_fit: "",
      urban_rural_fit: "",
      language_context_fit: "",
      buying_behaviour_fit: "",
      brand_safety_fit: "",
      objective_fit_primary: "",
      objective_fit_secondary: "",
      audience_age_skew: "",
      audience_gender_skew: "",
      content_environment: "",
      presenter_or_show_context: pkg.notes || "",
      primary_audience_tags: asTagString(record.audience_keywords),
      secondary_audience_tags: "",
      recommendation_tags: "",
      intelligence_notes: "",
      internal_key: `${record.id}|package|${pkg.name || "package"}`,
      source_type: "radio_package"
    });
  }

  return rows;
}

function createTvRows(record) {
  const rows = [];

  rows.push({
    ...createSharedRow(record, "channel"),
    channel_name: record.station,
    programme_name: "",
    slot: "",
    day_group: "",
    package_name: "",
    channel_tier: "",
    channel_format: "",
    genre_fit: "",
    audience_income_fit: "",
    premium_mass_fit: "",
    price_positioning_fit: "",
    youth_fit: "",
    family_fit: "",
    professional_fit: "",
    household_decision_maker_fit: "",
    high_value_client_fit: "",
    news_affairs_fit: "",
    sport_fit: "",
    entertainment_fit: "",
    appointment_viewing_fit: "",
    co_viewing_fit: "",
    language_context_fit: "",
    buying_behaviour_fit: "",
    brand_safety_fit: "",
    objective_fit_primary: "",
    objective_fit_secondary: "",
    audience_age_skew: "",
    audience_gender_skew: "",
    content_environment: "",
    programme_context: "",
    primary_audience_tags: asTagString(record.audience_keywords),
    secondary_audience_tags: "",
    recommendation_tags: "",
    intelligence_notes: record.intelligence_notes || "",
    internal_key: `${record.id}|channel`,
    source_type: "tv_channel"
  });

  for (const pkg of record.packages || []) {
    rows.push({
      ...createSharedRow(record, "package"),
      channel_name: record.station,
      programme_name: "",
      slot: "",
      day_group: "",
      package_name: pkg.name || "",
      channel_tier: "",
      channel_format: "",
      genre_fit: "",
      audience_income_fit: "",
      premium_mass_fit: "",
      price_positioning_fit: "",
      youth_fit: "",
      family_fit: "",
      professional_fit: "",
      household_decision_maker_fit: "",
      high_value_client_fit: "",
      news_affairs_fit: "",
      sport_fit: "",
      entertainment_fit: "",
      appointment_viewing_fit: "",
      co_viewing_fit: "",
      language_context_fit: "",
      buying_behaviour_fit: "",
      brand_safety_fit: "",
      objective_fit_primary: "",
      objective_fit_secondary: "",
      audience_age_skew: "",
      audience_gender_skew: "",
      content_environment: "",
      programme_context: pkg.notes || "",
      primary_audience_tags: asTagString(record.audience_keywords),
      secondary_audience_tags: "",
      recommendation_tags: "",
      intelligence_notes: "",
      internal_key: `${record.id}|package|${pkg.name || "package"}`,
      source_type: "tv_package"
    });
  }

  const pricing = record.pricing || [];
  if (Array.isArray(pricing)) {
    for (const entry of pricing) {
      const programmeName = entry.program || entry.programme || "";
      const slot = entry.slot || entry.time || "";
      const dayGroup = entry.group || entry.day_group || "";
      rows.push({
        ...createSharedRow(record, "slot"),
        channel_name: record.station,
        programme_name: programmeName,
        slot,
        day_group: dayGroup,
        package_name: "",
        channel_tier: "",
        channel_format: "",
        genre_fit: "",
        audience_income_fit: "",
        premium_mass_fit: "",
        price_positioning_fit: "",
        youth_fit: "",
        family_fit: "",
        professional_fit: "",
        household_decision_maker_fit: "",
        high_value_client_fit: "",
        news_affairs_fit: "",
        sport_fit: "",
        entertainment_fit: "",
        appointment_viewing_fit: "",
        co_viewing_fit: "",
        language_context_fit: "",
        buying_behaviour_fit: "",
        brand_safety_fit: "",
        objective_fit_primary: "",
        objective_fit_secondary: "",
        audience_age_skew: "",
        audience_gender_skew: "",
        content_environment: "",
        programme_context: "",
        primary_audience_tags: asTagString(record.audience_keywords),
        secondary_audience_tags: "",
        recommendation_tags: "",
        intelligence_notes: "",
        internal_key: `${record.id}|slot|${programmeName}|${slot}|${dayGroup}`,
        source_type: "tv_slot"
      });
    }
  } else {
    for (const [dayGroup, slots] of Object.entries(pricing)) {
      for (const slot of Object.keys(slots || {})) {
        rows.push({
          ...createSharedRow(record, "slot"),
          channel_name: record.station,
          programme_name: "",
          slot,
          day_group: dayGroup,
          package_name: "",
          channel_tier: "",
          channel_format: "",
          genre_fit: "",
          audience_income_fit: "",
          premium_mass_fit: "",
          price_positioning_fit: "",
          youth_fit: "",
          family_fit: "",
          professional_fit: "",
          household_decision_maker_fit: "",
          high_value_client_fit: "",
          news_affairs_fit: "",
          sport_fit: "",
          entertainment_fit: "",
          appointment_viewing_fit: "",
          co_viewing_fit: "",
          language_context_fit: "",
          buying_behaviour_fit: "",
          brand_safety_fit: "",
          objective_fit_primary: "",
          objective_fit_secondary: "",
          audience_age_skew: "",
          audience_gender_skew: "",
          content_environment: "",
          programme_context: "",
          primary_audience_tags: asTagString(record.audience_keywords),
          secondary_audience_tags: "",
          recommendation_tags: "",
          intelligence_notes: "",
          internal_key: `${record.id}|slot|${slot}|${dayGroup}`,
          source_type: "tv_slot"
        });
      }
    }
  }

  return rows;
}

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

const raw = fs.readFileSync(inventoryPath, "utf8");
const inventory = JSON.parse(raw);
const radioRecords = inventory.records.filter((record) => record.media_type === "radio");
const tvRecords = inventory.records.filter((record) => record.media_type === "tv");

ensureDir(radioOutputRoot);
ensureDir(tvOutputRoot);

const manifestRows = [];

for (const record of radioRecords) {
  const rows = createRadioRows(record);
  const fileName = `${sanitizeFileName(record.station)}.csv`;
  const relativePath = path.join("exports", "radio_inventory_intelligence", fileName);
  writeCsv(path.join(radioOutputRoot, fileName), radioColumns, rows);
  manifestRows.push({
    media_type: "radio",
    station_or_channel_name: record.station,
    media_outlet_code: record.id,
    row_count: rows.length,
    relative_path: relativePath
  });
}

for (const record of tvRecords) {
  const rows = createTvRows(record);
  const fileName = `${sanitizeFileName(record.station)}.csv`;
  const relativePath = path.join("exports", "tv_inventory_intelligence", fileName);
  writeCsv(path.join(tvOutputRoot, fileName), tvColumns, rows);
  manifestRows.push({
    media_type: "tv",
    station_or_channel_name: record.station,
    media_outlet_code: record.id,
    row_count: rows.length,
    relative_path: relativePath
  });
}

writeCsv(
  path.join(outputRoot, "broadcast_inventory_intelligence_manifest.csv"),
  ["media_type", "station_or_channel_name", "media_outlet_code", "row_count", "relative_path"],
  manifestRows
);

console.log(`Generated ${radioRecords.length} radio CSVs and ${tvRecords.length} TV CSVs.`);
