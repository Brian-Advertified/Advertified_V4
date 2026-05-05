import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, '..');

const inventoryDir = path.join(repoRoot, 'src', 'Advertified.App', 'App_Data', 'inventory');
const broadcastPlannerPath = path.join(inventoryDir, 'advertified-tv-radio-planner-data-with-targets-populated.json');
const socialSeedPath = path.join(repoRoot, 'database', 'bootstrap', '038_social_inventory_seed.sql');
const outputDir = path.join(repoRoot, 'exports');
const generatedDate = '2026-05-04';

const outputHtmlPath = path.join(outputDir, `advertified-inventory-catalogue-${generatedDate}.html`);
const outputPdfPath = path.join(outputDir, `advertified-inventory-catalogue-${generatedDate}.pdf`);

function parseCsv(content) {
  const rows = [];
  let row = [];
  let field = '';
  let inQuotes = false;

  if (content.charCodeAt(0) === 0xfeff) {
    content = content.slice(1);
  }

  for (let i = 0; i < content.length; i += 1) {
    const char = content[i];
    const next = content[i + 1];

    if (inQuotes) {
      if (char === '"' && next === '"') {
        field += '"';
        i += 1;
      } else if (char === '"') {
        inQuotes = false;
      } else {
        field += char;
      }
      continue;
    }

    if (char === '"') {
      inQuotes = true;
    } else if (char === ',') {
      row.push(field);
      field = '';
    } else if (char === '\n') {
      row.push(field);
      rows.push(row);
      row = [];
      field = '';
    } else if (char !== '\r') {
      field += char;
    }
  }

  if (field.length > 0 || row.length > 0) {
    row.push(field);
    rows.push(row);
  }

  const header = rows.shift() ?? [];
  return rows
    .filter(values => values.some(value => value.trim().length > 0))
    .map(values => Object.fromEntries(header.map((key, index) => [key, values[index] ?? ''])));
}

function readCsv(relativePath) {
  return parseCsv(fs.readFileSync(path.join(repoRoot, relativePath), 'utf8'));
}

function formatCurrency(value) {
  const number = Number(String(value ?? '').replace(/[^0-9.-]/g, ''));
  if (!Number.isFinite(number) || number === 0 && !String(value ?? '').match(/0/)) {
    return clean(value);
  }

  return `R ${number.toLocaleString('en-ZA', {
    maximumFractionDigits: Number.isInteger(number) ? 0 : 2,
  })}`;
}

function clean(value) {
  if (value === null || value === undefined) {
    return '';
  }

  if (Array.isArray(value)) {
    return value.filter(Boolean).join(', ');
  }

  return String(value)
    .replace(/_/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function compact(value, maxLength = 120) {
  const text = clean(value);
  if (text.length <= maxLength) {
    return text;
  }

  return `${text.slice(0, maxLength - 1).trimEnd()}...`;
}

function titleCase(value) {
  return clean(value)
    .split(' ')
    .map(part => (part.length > 0 ? `${part[0].toUpperCase()}${part.slice(1)}` : part))
    .join(' ');
}

function escapeHtml(value) {
  return clean(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function groupCount(rows, keySelector) {
  const map = new Map();
  for (const row of rows) {
    const key = clean(keySelector(row)) || 'Unspecified';
    map.set(key, (map.get(key) ?? 0) + 1);
  }

  return [...map.entries()]
    .map(([name, count]) => ({ name, count }))
    .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name));
}

function topList(rows, keySelector, limit = 8) {
  return groupCount(rows, keySelector)
    .slice(0, limit)
    .map(item => `${item.name} (${item.count})`)
    .join(', ');
}

function splitSqlTuples(valuesText) {
  const tuples = [];
  let start = -1;
  let depth = 0;
  let inQuote = false;

  for (let i = 0; i < valuesText.length; i += 1) {
    const char = valuesText[i];
    const next = valuesText[i + 1];

    if (inQuote) {
      if (char === "'" && next === "'") {
        i += 1;
      } else if (char === "'") {
        inQuote = false;
      }
      continue;
    }

    if (char === "'") {
      inQuote = true;
    } else if (char === '(') {
      if (depth === 0) {
        start = i;
      }
      depth += 1;
    } else if (char === ')') {
      depth -= 1;
      if (depth === 0 && start >= 0) {
        tuples.push(valuesText.slice(start + 1, i));
        start = -1;
      }
    }
  }

  return tuples;
}

function splitSqlFields(tupleText) {
  const fields = [];
  let field = '';
  let depth = 0;
  let inQuote = false;

  for (let i = 0; i < tupleText.length; i += 1) {
    const char = tupleText[i];
    const next = tupleText[i + 1];

    if (inQuote) {
      field += char;
      if (char === "'" && next === "'") {
        field += next;
        i += 1;
      } else if (char === "'") {
        inQuote = false;
      }
      continue;
    }

    if (char === "'") {
      inQuote = true;
      field += char;
    } else if (char === '(') {
      depth += 1;
      field += char;
    } else if (char === ')') {
      depth -= 1;
      field += char;
    } else if (char === ',' && depth === 0) {
      fields.push(field.trim());
      field = '';
    } else {
      field += char;
    }
  }

  if (field.trim().length > 0) {
    fields.push(field.trim());
  }

  return fields;
}

function parseSqlLiteral(field) {
  const value = field.trim();

  if (/^null$/i.test(value)) {
    return null;
  }

  if (/^true$/i.test(value)) {
    return true;
  }

  if (/^false$/i.test(value)) {
    return false;
  }

  const dateMatch = value.match(/^DATE\s+'([^']+)'$/i);
  if (dateMatch) {
    return dateMatch[1];
  }

  const codeMatch = value.match(/WHERE\s+code\s*=\s*'([^']+)'/i);
  if (codeMatch) {
    return codeMatch[1];
  }

  if (value.startsWith("'")) {
    let literal = '';
    for (let i = 1; i < value.length; i += 1) {
      const char = value[i];
      const next = value[i + 1];
      if (char === "'" && next === "'") {
        literal += "'";
        i += 1;
      } else if (char === "'") {
        return literal;
      } else {
        literal += char;
      }
    }
    return literal;
  }

  const number = Number(value);
  if (Number.isFinite(number)) {
    return number;
  }

  return value;
}

function extractInsertValues(sql, tableName, endMarker) {
  const insertIndex = sql.indexOf(`INSERT INTO ${tableName}`);
  if (insertIndex < 0) {
    return [];
  }

  const valuesIndex = sql.indexOf('VALUES', insertIndex);
  const endIndex = endMarker
    ? sql.indexOf(endMarker, valuesIndex)
    : sql.indexOf(';', valuesIndex);

  if (valuesIndex < 0 || endIndex < 0) {
    return [];
  }

  return splitSqlTuples(sql.slice(valuesIndex + 'VALUES'.length, endIndex));
}

function parseJsonMaybe(value) {
  try {
    return value ? JSON.parse(value) : {};
  } catch {
    return {};
  }
}

function parseSocialSeed() {
  const sql = fs.readFileSync(socialSeedPath, 'utf8');

  const outlets = extractInsertValues(sql, 'media_outlet', 'ON CONFLICT')
    .map(tuple => splitSqlFields(tuple).map(parseSqlLiteral))
    .map(fields => {
      const source = parseJsonMaybe(fields[14]);
      const strategy = parseJsonMaybe(fields[15]);
      return {
        code: fields[1],
        name: fields[2],
        mediaType: fields[3],
        coverageType: fields[4],
        health: fields[5],
        operator: fields[6],
        languageNotes: fields[9],
        age: fields[10],
        gender: fields[11],
        lsm: fields[12],
        targetAudience: fields[13],
        billingModel: source.billing_model,
        platform: source.platform,
        objectivePrimary: strategy.objective_fit_primary,
        objectiveSecondary: strategy.objective_fit_secondary,
        environment: strategy.environment_type,
        notes: strategy.intelligence_notes,
      };
    });

  const packages = extractInsertValues(sql, 'media_outlet_pricing_package')
    .map(tuple => splitSqlFields(tuple).map(parseSqlLiteral))
    .map(fields => {
      const notes = parseJsonMaybe(fields[10]);
      return {
        outletCode: fields[1],
        packageName: fields[2],
        packageType: fields[3],
        valueZar: fields[6],
        investmentZar: fields[7],
        costPerMonthZar: fields[8],
        durationMonths: fields[9],
        billingEvent: notes.billing_event,
        benchmarkCpm: [notes.benchmark_cpm_min_zar, notes.benchmark_cpm_max_zar].filter(value => value !== undefined).join(' - '),
        benchmarkCpc: [notes.benchmark_cpc_min_zar, notes.benchmark_cpc_max_zar].filter(value => value !== undefined).join(' - '),
        sourceName: fields[11],
        sourceDate: fields[12],
      };
    });

  return { outlets, packages };
}

function table(headers, rows, options = {}) {
  const columns = options.columns ?? headers.map(header => ({
    label: header,
    value: row => row[header],
  }));

  const body = rows.map(row => `
    <tr>
      ${columns.map(column => `<td>${escapeHtml(column.value(row))}</td>`).join('')}
    </tr>`).join('');

  return `
    <table class="${options.className ?? ''}">
      <thead>
        <tr>${columns.map(column => `<th>${escapeHtml(column.label)}</th>`).join('')}</tr>
      </thead>
      <tbody>${body}</tbody>
    </table>`;
}

function stat(label, value, hint = '') {
  return `
    <div class="stat">
      <div class="stat-value">${escapeHtml(value)}</div>
      <div class="stat-label">${escapeHtml(label)}</div>
      ${hint ? `<div class="stat-hint">${escapeHtml(hint)}</div>` : ''}
    </div>`;
}

function section(title, subtitle, content, className = '') {
  return `
    <section class="${className}">
      <div class="section-heading">
        <h2>${escapeHtml(title)}</h2>
        ${subtitle ? `<p>${escapeHtml(subtitle)}</p>` : ''}
      </div>
      ${content}
    </section>`;
}

function sourceNote(fileName, count) {
  return `${count.toLocaleString('en-ZA')} rows from ${fileName}`;
}

function buildHtml() {
  const oohRows = readCsv('src/Advertified.App/App_Data/inventory/ooh_inventory_enriched.csv');
  const radioRows = readCsv('src/Advertified.App/App_Data/inventory/radio_final_enriched.csv');
  const newspaperRows = readCsv('src/Advertified.App/App_Data/inventory/newspaper_media_planner_2026.csv');
  const planner = JSON.parse(fs.readFileSync(broadcastPlannerPath, 'utf8'));
  const social = parseSocialSeed();

  const billboardRows = oohRows.filter(row => /billboard/i.test(`${row['Media Type']} ${row['Site Name']}`));
  const radioStations = radioRows.filter(row => row.row_kind === 'station').length;
  const radioSlots = radioRows.filter(row => row.row_kind === 'slot').length;
  const radioPackages = radioRows.filter(row => row.row_kind === 'package').length;
  const tvPackages = planner.tv?.packages ?? [];
  const tvShows = planner.tv?.showHighlights ?? [];

  const summaryRows = [
    {
      channel: 'OOH / Billboards',
      count: oohRows.length,
      source: 'ooh_inventory_enriched.csv',
      notes: `${billboardRows.length} billboard-specific rows. Top media types: ${topList(oohRows, row => row['Media Type'], 5)}.`,
    },
    {
      channel: 'Radio',
      count: radioRows.length,
      source: 'radio_final_enriched.csv',
      notes: `${radioStations} station rows, ${radioSlots} slot rows, ${radioPackages} package rows. Top stations: ${topList(radioRows, row => row.station_name, 5)}.`,
    },
    {
      channel: 'TV',
      count: tvPackages.length + tvShows.length,
      source: 'advertified-tv-radio-planner-data-with-targets-populated.json',
      notes: `${tvPackages.length} TV packages and ${tvShows.length} show highlights from SABC planner data.`,
    },
    {
      channel: 'Newspaper',
      count: newspaperRows.length,
      source: 'newspaper_media_planner_2026.csv',
      notes: `Top publications: ${topList(newspaperRows, row => row.Publication, 5)}.`,
    },
    {
      channel: 'Social Media',
      count: social.outlets.length + social.packages.length,
      source: '038_social_inventory_seed.sql',
      notes: `${social.outlets.length} platforms and ${social.packages.length} benchmark package rows.`,
    },
  ];

  const oohColumns = [
    { label: 'Supplier', value: row => row.Supplier },
    { label: 'Code', value: row => row['Site Code'] },
    { label: 'Site / Placement', value: row => compact(row['Site Name'], 72) },
    { label: 'City', value: row => row.City },
    { label: 'Area', value: row => compact(row['Suburb / Area'], 42) },
    { label: 'Province', value: row => row.Province },
    { label: 'Media Type', value: row => row['Media Type'] },
    { label: 'Available', value: row => titleCase(row['Available Now']) },
    { label: 'Monthly Rate', value: row => formatCurrency(row['Monthly Rate (ZAR)'] || row['Discounted Rate (ZAR)']) },
    { label: 'Traffic', value: row => clean(row['Traffic Count']) },
  ];

  const radioColumns = [
    { label: 'Station', value: row => row.station_name },
    { label: 'Kind', value: row => titleCase(row.row_kind) },
    { label: 'Day / Package', value: row => row.package_name || titleCase(row.day_group) },
    { label: 'Slot', value: row => row.slot },
    { label: 'Coverage', value: row => titleCase(row.coverage_type) },
    { label: 'Province / City', value: row => compact([row.province_codes, row.city_labels].filter(Boolean).join(' / '), 60) },
    { label: 'Language', value: row => compact(row.language_codes, 44) },
    { label: 'Audience', value: row => compact(row.target_audience, 70) },
    { label: 'Fit', value: row => compact([row.daypart_refined, row.content_type, row.best_campaign_objective].filter(Boolean).join(' / '), 64) },
  ];

  const tvPackageColumns = [
    { label: 'Package', value: row => compact(row.name, 82) },
    { label: 'Vendor', value: row => row.vendor },
    { label: 'Channel', value: row => row.channelFamily },
    { label: 'Content', value: row => titleCase(row.contentType) },
    { label: 'Audience', value: row => row.audienceSegment },
    { label: 'Duration', value: row => row.duration },
    { label: 'Spots', value: row => row.spots },
    { label: 'Package Cost', value: row => formatCurrency(row.packageCostZAR) },
    { label: 'Use Cases', value: row => compact(row.plannerUseCases, 80) },
  ];

  const tvShowColumns = [
    { label: 'Channel', value: row => row.channel },
    { label: 'Show', value: row => row.show },
    { label: 'Genre', value: row => row.genre },
    { label: 'Schedule', value: row => row.schedule },
    { label: 'Audience Summary', value: row => compact(row.audienceSummary, 120) },
  ];

  const newspaperColumns = [
    { label: 'ID', value: row => row['Inventory ID'] },
    { label: 'Publisher', value: row => row.Publisher },
    { label: 'Publication', value: row => row.Publication },
    { label: 'Category', value: row => row.Category },
    { label: 'Product', value: row => compact(row['Ad Product'], 72) },
    { label: 'Type', value: row => row['Colour/Type'] },
    { label: 'Billing', value: row => row['Billing Unit'] },
    { label: 'Base Rate', value: row => formatCurrency(row['Base Rate (R excl VAT)']) },
    { label: 'Run', value: row => row['Region/Run'] },
    { label: 'Specs', value: row => compact(row['Specs / Size'], 44) },
  ];

  const socialOutletColumns = [
    { label: 'Platform', value: row => row.name },
    { label: 'Operator', value: row => row.operator },
    { label: 'Coverage', value: row => titleCase(row.coverageType) },
    { label: 'Audience', value: row => row.targetAudience },
    { label: 'Age', value: row => row.age },
    { label: 'LSM', value: row => row.lsm },
    { label: 'Primary Objective', value: row => titleCase(row.objectivePrimary) },
    { label: 'Environment', value: row => titleCase(row.environment) },
    { label: 'Notes', value: row => compact(row.notes, 105) },
  ];

  const socialPackageColumns = [
    { label: 'Platform', value: row => (social.outlets.find(outlet => outlet.code === row.outletCode)?.name ?? row.outletCode) },
    { label: 'Package', value: row => row.packageName },
    { label: 'Type', value: row => titleCase(row.packageType) },
    { label: 'Investment', value: row => formatCurrency(row.investmentZar) },
    { label: 'Monthly Cost', value: row => formatCurrency(row.costPerMonthZar) },
    { label: 'Duration', value: row => `${row.durationMonths} month` },
    { label: 'Billing', value: row => titleCase(row.billingEvent) },
    { label: 'CPM Benchmark', value: row => row.benchmarkCpm ? `R ${row.benchmarkCpm}` : '' },
    { label: 'Source', value: row => compact(row.sourceName, 70) },
  ];

  const stats = [
    stat('Total Inventory Rows', summaryRows.reduce((total, row) => total + row.count, 0).toLocaleString('en-ZA'), 'across source files and seed data'),
    stat('OOH / Billboards', oohRows.length.toLocaleString('en-ZA'), `${billboardRows.length} billboard-specific`),
    stat('Radio', radioRows.length.toLocaleString('en-ZA'), `${radioStations} stations, ${radioSlots} slots`),
    stat('TV', (tvPackages.length + tvShows.length).toLocaleString('en-ZA'), `${tvPackages.length} packages`),
    stat('Print', newspaperRows.length.toLocaleString('en-ZA'), 'newspaper products'),
    stat('Social', (social.outlets.length + social.packages.length).toLocaleString('en-ZA'), 'platforms and benchmark packages'),
  ].join('');

  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Advertified Inventory Catalogue ${generatedDate}</title>
  <style>
    @page {
      size: A4 landscape;
      margin: 12mm 10mm 13mm;
    }

    * {
      box-sizing: border-box;
    }

    body {
      margin: 0;
      color: #1f2933;
      background: #ffffff;
      font-family: Arial, Helvetica, sans-serif;
      font-size: 8.4px;
      line-height: 1.35;
    }

    .cover {
      min-height: 178mm;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      padding: 6mm 4mm;
      page-break-after: always;
      background: linear-gradient(135deg, #0f172a 0%, #143642 46%, #175c55 100%);
      color: #fff;
    }

    .brand {
      font-size: 11px;
      font-weight: 700;
      letter-spacing: .12em;
      text-transform: uppercase;
      color: #c7f9ea;
    }

    h1 {
      margin: 26mm 0 0;
      width: 76%;
      font-size: 37px;
      line-height: 1.03;
      letter-spacing: 0;
    }

    .cover p {
      width: 72%;
      margin: 8mm 0 0;
      color: #e5eef0;
      font-size: 13px;
    }

    .meta {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 6mm;
      color: #d8f4ee;
      font-size: 10px;
    }

    .meta strong {
      display: block;
      color: #ffffff;
      font-size: 16px;
      margin-bottom: 2mm;
    }

    section {
      page-break-before: always;
    }

    .section-heading {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 12mm;
      margin: 0 0 5mm;
      border-bottom: 1px solid #cbd5df;
      padding-bottom: 3mm;
    }

    h2 {
      margin: 0;
      color: #102a43;
      font-size: 18px;
      line-height: 1.1;
      letter-spacing: 0;
    }

    h3 {
      margin: 7mm 0 3mm;
      color: #243b53;
      font-size: 12px;
      letter-spacing: 0;
    }

    .section-heading p {
      max-width: 58%;
      margin: 0;
      color: #52606d;
      text-align: right;
      font-size: 9px;
    }

    .stats {
      display: grid;
      grid-template-columns: repeat(6, 1fr);
      gap: 3mm;
      margin-bottom: 6mm;
    }

    .stat {
      min-height: 23mm;
      padding: 3mm;
      border: 1px solid #d9e2ec;
      border-left: 3px solid #168b77;
      background: #f8fbfb;
      break-inside: avoid;
    }

    .stat-value {
      color: #102a43;
      font-size: 18px;
      font-weight: 700;
      line-height: 1;
    }

    .stat-label {
      margin-top: 2mm;
      color: #334e68;
      font-weight: 700;
      text-transform: uppercase;
      font-size: 7px;
    }

    .stat-hint {
      margin-top: 1.5mm;
      color: #627d98;
      font-size: 7.3px;
    }

    table {
      width: 100%;
      border-collapse: collapse;
      table-layout: fixed;
      page-break-inside: auto;
    }

    thead {
      display: table-header-group;
    }

    tr {
      page-break-inside: avoid;
      page-break-after: auto;
    }

    th {
      background: #102a43;
      color: #fff;
      padding: 4px 5px;
      border: 1px solid #102a43;
      text-align: left;
      font-size: 7.2px;
      line-height: 1.2;
      text-transform: uppercase;
    }

    td {
      padding: 4px 5px;
      border: 1px solid #d9e2ec;
      vertical-align: top;
      word-break: break-word;
      overflow-wrap: anywhere;
    }

    tbody tr:nth-child(even) td {
      background: #f7fafc;
    }

    .summary-table th:nth-child(1) { width: 16%; }
    .summary-table th:nth-child(2) { width: 8%; }
    .summary-table th:nth-child(3) { width: 28%; }
    .summary-table th:nth-child(4) { width: 48%; }

    .ooh-table th:nth-child(1) { width: 8%; }
    .ooh-table th:nth-child(2) { width: 6%; }
    .ooh-table th:nth-child(3) { width: 21%; }
    .ooh-table th:nth-child(4) { width: 9%; }
    .ooh-table th:nth-child(5) { width: 10%; }
    .ooh-table th:nth-child(6) { width: 9%; }
    .ooh-table th:nth-child(7) { width: 13%; }
    .ooh-table th:nth-child(8) { width: 7%; }
    .ooh-table th:nth-child(9) { width: 9%; }
    .ooh-table th:nth-child(10) { width: 8%; }

    .radio-table {
      font-size: 6.3px;
      line-height: 1.2;
    }

    .radio-table th,
    .radio-table td {
      padding: 3px 3px;
    }

    .radio-table th:nth-child(1) { width: 10%; }
    .radio-table th:nth-child(2) { width: 5%; }
    .radio-table th:nth-child(3) { width: 13%; }
    .radio-table th:nth-child(4) { width: 8%; }
    .radio-table th:nth-child(5) { width: 7%; }
    .radio-table th:nth-child(6) { width: 14%; }
    .radio-table th:nth-child(7) { width: 11%; }
    .radio-table th:nth-child(8) { width: 18%; }
    .radio-table th:nth-child(9) { width: 14%; }

    .tv-table th:nth-child(1) { width: 23%; }
    .tv-table th:nth-child(2) { width: 8%; }
    .tv-table th:nth-child(3) { width: 12%; }
    .tv-table th:nth-child(4) { width: 8%; }
    .tv-table th:nth-child(5) { width: 9%; }
    .tv-table th:nth-child(6) { width: 7%; }
    .tv-table th:nth-child(7) { width: 6%; }
    .tv-table th:nth-child(8) { width: 10%; }
    .tv-table th:nth-child(9) { width: 17%; }

    .print-table {
      font-size: 7.1px;
    }

    .print-table th:nth-child(1) { width: 7%; }
    .print-table th:nth-child(2) { width: 10%; }
    .print-table th:nth-child(3) { width: 11%; }
    .print-table th:nth-child(4) { width: 9%; }
    .print-table th:nth-child(5) { width: 21%; }
    .print-table th:nth-child(6) { width: 8%; }
    .print-table th:nth-child(7) { width: 7%; }
    .print-table th:nth-child(8) { width: 8%; }
    .print-table th:nth-child(9) { width: 8%; }
    .print-table th:nth-child(10) { width: 11%; }

    .social-table th:nth-child(1) { width: 13%; }
    .social-table th:nth-child(2) { width: 10%; }
    .social-table th:nth-child(3) { width: 8%; }
    .social-table th:nth-child(4) { width: 13%; }
    .social-table th:nth-child(5) { width: 7%; }
    .social-table th:nth-child(6) { width: 7%; }
    .social-table th:nth-child(7) { width: 10%; }
    .social-table th:nth-child(8) { width: 10%; }
    .social-table th:nth-child(9) { width: 22%; }

    .source-line {
      margin: 0 0 4mm;
      color: #627d98;
      font-size: 8px;
    }

    .note {
      margin-top: 4mm;
      color: #52606d;
      font-size: 8px;
    }
  </style>
</head>
<body>
  <div class="cover">
    <div>
      <div class="brand">Advertified</div>
      <h1>Inventory Catalogue</h1>
      <p>Billboards, Digital Screens, radio, TV, newspaper, and social media inventory compiled from the project source data and seed files.</p>
    </div>
    <div class="meta">
      <div><strong>${generatedDate}</strong>Generated date</div>
      <div><strong>${summaryRows.reduce((total, row) => total + row.count, 0).toLocaleString('en-ZA')}</strong>Total listed rows</div>
      <div><strong>5</strong>Source datasets</div>
    </div>
  </div>

  ${section('Inventory Summary', 'High-level count by source and channel.', `
    <div class="stats">${stats}</div>
    ${table([], summaryRows, {
      className: 'summary-table',
      columns: [
        { label: 'Channel', value: row => row.channel },
        { label: 'Rows', value: row => row.count.toLocaleString('en-ZA') },
        { label: 'Source', value: row => row.source },
        { label: 'Notes', value: row => row.notes },
      ],
    })}
    <p class="note">The radio section is intentionally dense because it includes every enriched station, slot, and package row available in the source CSV.</p>
  `, 'summary')}

  ${section('OOH And Billboards', sourceNote('ooh_inventory_enriched.csv', oohRows.length), `
    <p class="source-line">Billboard-specific rows: ${billboardRows.length.toLocaleString('en-ZA')}. Provinces: ${topList(oohRows, row => row.Province, 8)}.</p>
    ${table([], oohRows, { className: 'ooh-table', columns: oohColumns })}
  `)}

  ${section('Radio Inventory', sourceNote('radio_final_enriched.csv', radioRows.length), `
    <p class="source-line">Station rows: ${radioStations.toLocaleString('en-ZA')}; slot rows: ${radioSlots.toLocaleString('en-ZA')}; package rows: ${radioPackages.toLocaleString('en-ZA')}.</p>
    ${table([], radioRows, { className: 'radio-table', columns: radioColumns })}
  `)}

  ${section('TV Inventory', sourceNote('advertified-tv-radio-planner-data-with-targets-populated.json', tvPackages.length + tvShows.length), `
    <h3>TV Packages</h3>
    ${table([], tvPackages, { className: 'tv-table', columns: tvPackageColumns })}
    <h3>TV Show Highlights</h3>
    ${table([], tvShows, { className: 'tv-show-table', columns: tvShowColumns })}
  `)}

  ${section('Newspaper Inventory', sourceNote('newspaper_media_planner_2026.csv', newspaperRows.length), `
    <p class="source-line">Publishers: ${topList(newspaperRows, row => row.Publisher, 8)}.</p>
    ${table([], newspaperRows, { className: 'print-table', columns: newspaperColumns })}
  `)}

  ${section('Social Media Inventory', sourceNote('038_social_inventory_seed.sql', social.outlets.length + social.packages.length), `
    <h3>Platforms</h3>
    ${table([], social.outlets, { className: 'social-table', columns: socialOutletColumns })}
    <h3>Benchmark Packages</h3>
    ${table([], social.packages, { className: 'social-package-table', columns: socialPackageColumns })}
  `)}
</body>
</html>`;
}

async function renderPdf(html) {
  const require = createRequire(pathToFileURL(path.join(repoRoot, 'src', 'Advertified.Web', 'package.json')));
  const { chromium } = require('playwright');
  const browser = await chromium.launch();

  try {
    const page = await browser.newPage({ viewport: { width: 1600, height: 1100 } });
    await page.setContent(html, { waitUntil: 'load' });
    await page.pdf({
      path: outputPdfPath,
      format: 'A4',
      landscape: true,
      printBackground: true,
      margin: {
        top: '12mm',
        right: '10mm',
        bottom: '13mm',
        left: '10mm',
      },
      displayHeaderFooter: true,
      headerTemplate: '<div></div>',
      footerTemplate: `
        <div style="width:100%;font-family:Arial,Helvetica,sans-serif;font-size:7px;color:#627d98;padding:0 10mm;display:flex;justify-content:space-between;">
          <span>Advertified inventory catalogue</span>
          <span>Page <span class="pageNumber"></span> of <span class="totalPages"></span></span>
        </div>`,
    });
  } finally {
    await browser.close();
  }
}

async function main() {
  fs.mkdirSync(outputDir, { recursive: true });
  const html = buildHtml();
  fs.writeFileSync(outputHtmlPath, html, 'utf8');
  await renderPdf(html);

  const pdfSize = fs.statSync(outputPdfPath).size;
  console.log(`Wrote ${path.relative(repoRoot, outputPdfPath)} (${pdfSize.toLocaleString('en-ZA')} bytes)`);
  console.log(`Wrote ${path.relative(repoRoot, outputHtmlPath)}`);
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
