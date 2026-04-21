var LEAD_CONTROL_HEADERS = [
  'record_key',
  'item_type',
  'item_label',
  'campaign_id',
  'prospect_lead_id',
  'lead_id',
  'lead_action_id',
  'title',
  'subtitle',
  'description',
  'unified_status',
  'assigned_agent_user_id',
  'assigned_agent_name',
  'is_assigned_to_current_user',
  'is_unassigned',
  'is_urgent',
  'route_path',
  'route_url',
  'route_label',
  'created_at',
  'updated_at',
  'due_at',
  'snapshot_generated_at',
  'snapshot_status',
  'last_seen_at'
];

var LEAD_TOTAL_HEADERS = [
  'metric',
  'value',
  'snapshot_generated_at',
  'updated_at'
];

function doGet(e) {
  try {
    var config = getLeadControlConfig_();
    var payload = {
      ok: true,
      sheetName: config.sheetName,
      totalsSheetName: config.totalsSheetName,
      archiveMissingItems: config.archiveMissingItems,
      hasToken: !!config.token
    };

    return jsonResponse_(payload);
  } catch (error) {
    return jsonResponse_({
      ok: false,
      error: error.message
    });
  }
}

function doPost(e) {
  try {
    var config = getLeadControlConfig_();
    validateWebhookToken_(e, config);

    var body = parseWebhookBody_(e);
    validateWebhookPayload_(body);

    var spreadsheet = SpreadsheetApp.getActiveSpreadsheet();
    var towerSheet = ensureSheet_(spreadsheet, config.sheetName, LEAD_CONTROL_HEADERS);
    var totalsSheet = ensureSheet_(spreadsheet, config.totalsSheetName, LEAD_TOTAL_HEADERS);
    var snapshotGeneratedAt = normalizeIsoString_(body.generatedAtUtc) || new Date().toISOString();
    var items = Array.isArray(body.items) ? body.items : [];

    upsertLeadControlRows_(towerSheet, items, snapshotGeneratedAt, config);
    upsertTotalsRows_(totalsSheet, body.totals || {}, snapshotGeneratedAt);

    return jsonResponse_({
      ok: true,
      exportedItemCount: items.length,
      generatedAtUtc: snapshotGeneratedAt
    });
  } catch (error) {
    return jsonResponse_({
      ok: false,
      error: error.message
    });
  }
}

function setupLeadControlTower() {
  var config = getLeadControlConfig_();
  var spreadsheet = SpreadsheetApp.getActiveSpreadsheet();
  ensureSheet_(spreadsheet, config.sheetName, LEAD_CONTROL_HEADERS);
  ensureSheet_(spreadsheet, config.totalsSheetName, LEAD_TOTAL_HEADERS);
}

function getLeadControlConfig_() {
  var properties = PropertiesService.getScriptProperties();
  return {
    sheetName: properties.getProperty('LEAD_CONTROL_TOWER_SHEET_NAME') || 'Lead Control Tower',
    totalsSheetName: properties.getProperty('LEAD_CONTROL_TOTALS_SHEET_NAME') || 'Lead Control Totals',
    token: properties.getProperty('LEAD_CONTROL_WEBHOOK_TOKEN') || '',
    appBaseUrl: trimTrailingSlash_(properties.getProperty('LEAD_CONTROL_APP_BASE_URL') || ''),
    archiveMissingItems: (properties.getProperty('LEAD_CONTROL_ARCHIVE_MISSING_ITEMS') || 'true').toLowerCase() !== 'false'
  };
}

function validateWebhookToken_(e, config) {
  if (!config.token) {
    return;
  }

  var providedToken = e && e.parameter ? (e.parameter.token || '') : '';
  if (!providedToken || providedToken !== config.token) {
    throw new Error('Unauthorized webhook call.');
  }
}

function parseWebhookBody_(e) {
  if (!e || !e.postData || !e.postData.contents) {
    throw new Error('Webhook body is empty.');
  }

  try {
    return JSON.parse(e.postData.contents);
  } catch (error) {
    throw new Error('Webhook body is not valid JSON.');
  }
}

function validateWebhookPayload_(body) {
  if (!body || typeof body !== 'object') {
    throw new Error('Webhook payload is missing.');
  }

  if (!Array.isArray(body.items)) {
    throw new Error('Webhook payload must include an items array.');
  }

  if (!body.totals || typeof body.totals !== 'object') {
    throw new Error('Webhook payload must include totals.');
  }
}

function ensureSheet_(spreadsheet, sheetName, headers) {
  var sheet = spreadsheet.getSheetByName(sheetName);
  if (!sheet) {
    sheet = spreadsheet.insertSheet(sheetName);
  }

  var headerRange = sheet.getRange(1, 1, 1, headers.length);
  var currentHeaders = sheet.getLastRow() > 0
    ? headerRange.getValues()[0]
    : [];

  var shouldResetHeaders = currentHeaders.length !== headers.length
    || headers.some(function (header, index) {
      return currentHeaders[index] !== header;
    });

  if (shouldResetHeaders) {
    headerRange.setValues([headers]);
    headerRange.setFontWeight('bold');
    sheet.setFrozenRows(1);
  }

  if (sheet.getFrozenRows() !== 1) {
    sheet.setFrozenRows(1);
  }

  return sheet;
}

function upsertLeadControlRows_(sheet, items, snapshotGeneratedAt, config) {
  var headerIndex = buildHeaderIndex_(LEAD_CONTROL_HEADERS);
  var existingRows = getExistingDataRows_(sheet, LEAD_CONTROL_HEADERS.length);
  var existingRowMap = {};

  existingRows.forEach(function (row, offsetIndex) {
    var key = sanitizeCell_(row[headerIndex.record_key]);
    if (key) {
      existingRowMap[key] = {
        rowNumber: offsetIndex + 2,
        values: row
      };
    }
  });

  var seenKeys = {};
  items.forEach(function (item) {
    var row = buildLeadControlRow_(item, snapshotGeneratedAt, config);
    var key = row[headerIndex.record_key];
    if (!key) {
      return;
    }

    seenKeys[key] = true;

    if (existingRowMap[key]) {
      sheet.getRange(existingRowMap[key].rowNumber, 1, 1, LEAD_CONTROL_HEADERS.length).setValues([row]);
      delete existingRowMap[key];
      return;
    }

    sheet.appendRow(row);
  });

  if (config.archiveMissingItems) {
    archiveMissingLeadControlRows_(sheet, existingRowMap, headerIndex, snapshotGeneratedAt);
  }

  autosizeSheet_(sheet, LEAD_CONTROL_HEADERS.length);
}

function buildLeadControlRow_(item, snapshotGeneratedAt, config) {
  var routePath = sanitizeCell_(item.routePath);
  var routeUrl = routePath && config.appBaseUrl
    ? config.appBaseUrl + routePath
    : routePath;

  return [
    sanitizeCell_(item.id),
    sanitizeCell_(item.itemType),
    sanitizeCell_(item.itemLabel),
    sanitizeCell_(item.campaignId),
    sanitizeCell_(item.prospectLeadId),
    sanitizeCell_(item.leadId),
    sanitizeCell_(item.leadActionId),
    sanitizeCell_(item.title),
    sanitizeCell_(item.subtitle),
    sanitizeCell_(item.description),
    sanitizeCell_(item.unifiedStatus),
    sanitizeCell_(item.assignedAgentUserId),
    sanitizeCell_(item.assignedAgentName),
    toBooleanString_(item.isAssignedToCurrentUser),
    toBooleanString_(item.isUnassigned),
    toBooleanString_(item.isUrgent),
    routePath,
    routeUrl,
    sanitizeCell_(item.routeLabel),
    normalizeIsoString_(item.createdAt),
    normalizeIsoString_(item.updatedAt),
    normalizeIsoString_(item.dueAt),
    snapshotGeneratedAt,
    'active',
    snapshotGeneratedAt
  ];
}

function archiveMissingLeadControlRows_(sheet, missingRowMap, headerIndex, snapshotGeneratedAt) {
  Object.keys(missingRowMap).forEach(function (key) {
    var rowInfo = missingRowMap[key];
    var row = rowInfo.values.slice();
    row[headerIndex.snapshot_generated_at] = snapshotGeneratedAt;
    row[headerIndex.snapshot_status] = 'missing_from_snapshot';
    row[headerIndex.last_seen_at] = snapshotGeneratedAt;
    sheet.getRange(rowInfo.rowNumber, 1, 1, LEAD_CONTROL_HEADERS.length).setValues([row]);
  });
}

function upsertTotalsRows_(sheet, totals, snapshotGeneratedAt) {
  var headerIndex = buildHeaderIndex_(LEAD_TOTAL_HEADERS);
  var existingRows = getExistingDataRows_(sheet, LEAD_TOTAL_HEADERS.length);
  var existingRowMap = {};

  existingRows.forEach(function (row, offsetIndex) {
    var key = sanitizeCell_(row[headerIndex.metric]);
    if (key) {
      existingRowMap[key] = offsetIndex + 2;
    }
  });

  var metrics = [
    { metric: 'total_items', value: totals.totalItems },
    { metric: 'urgent_count', value: totals.urgentCount },
    { metric: 'assigned_to_me_count', value: totals.assignedToMeCount },
    { metric: 'unassigned_count', value: totals.unassignedCount },
    { metric: 'new_inbound_prospects_count', value: totals.newInboundProspectsCount },
    { metric: 'unassigned_prospects_count', value: totals.unassignedProspectsCount },
    { metric: 'open_lead_actions_count', value: totals.openLeadActionsCount },
    { metric: 'no_recent_activity_count', value: totals.noRecentActivityCount },
    { metric: 'awaiting_client_responses_count', value: totals.awaitingClientResponsesCount },
    { metric: 'overdue_follow_ups_count', value: totals.overdueFollowUpsCount }
  ];

  metrics.forEach(function (metric) {
    var row = [
      metric.metric,
      metric.value == null ? '' : metric.value,
      snapshotGeneratedAt,
      new Date().toISOString()
    ];

    if (existingRowMap[metric.metric]) {
      sheet.getRange(existingRowMap[metric.metric], 1, 1, LEAD_TOTAL_HEADERS.length).setValues([row]);
      delete existingRowMap[metric.metric];
      return;
    }

    sheet.appendRow(row);
  });

  autosizeSheet_(sheet, LEAD_TOTAL_HEADERS.length);
}

function getExistingDataRows_(sheet, columnCount) {
  var lastRow = sheet.getLastRow();
  if (lastRow < 2) {
    return [];
  }

  return sheet.getRange(2, 1, lastRow - 1, columnCount).getValues();
}

function buildHeaderIndex_(headers) {
  var index = {};
  headers.forEach(function (header, position) {
    index[header] = position;
  });
  return index;
}

function autosizeSheet_(sheet, columnCount) {
  sheet.autoResizeColumns(1, columnCount);
}

function trimTrailingSlash_(value) {
  return value ? value.replace(/\/+$/, '') : '';
}

function sanitizeCell_(value) {
  return value == null ? '' : String(value);
}

function toBooleanString_(value) {
  return value ? 'true' : 'false';
}

function normalizeIsoString_(value) {
  if (!value) {
    return '';
  }

  var date = new Date(value);
  if (isNaN(date.getTime())) {
    return String(value);
  }

  return date.toISOString();
}

function jsonResponse_(payload) {
  return ContentService
    .createTextOutput(JSON.stringify(payload))
    .setMimeType(ContentService.MimeType.JSON);
}
