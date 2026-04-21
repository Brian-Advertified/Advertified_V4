var LEAD_CONTROL_SYNC_HEADERS = [
  'record_key',
  'lead_id',
  'lead_name',
  'location',
  'category',
  'source',
  'source_reference',
  'unified_status',
  'owner_agent_user_id',
  'owner_agent_name',
  'owner_resolution',
  'assignment_status',
  'has_been_contacted',
  'first_contacted_at',
  'contact_status',
  'last_contacted_at',
  'next_action',
  'next_action_due_at',
  'next_follow_up_at',
  'sla_due_at',
  'priority',
  'attention_reasons',
  'open_lead_action_count',
  'has_prospect',
  'prospect_lead_id',
  'active_campaign_id',
  'won_campaign_id',
  'converted_to_sale',
  'last_outcome',
  'route_path',
  'route_url',
  'snapshot_generated_at',
  'snapshot_status',
  'last_seen_at'
];

var LEAD_CONTROL_HEADERS = [
  'record_key',
  'lead_name',
  'location',
  'category',
  'source',
  'owner',
  'assignment_status',
  'lifecycle_stage',
  'contact_status',
  'next_action',
  'next_action_due_at',
  'next_follow_up_at',
  'sla_due_at',
  'priority',
  'attention_reasons',
  'last_outcome',
  'notes',
  'open_in_advertified',
  'last_updated_at'
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
    return jsonResponse_({
      ok: true,
      sheetName: config.sheetName,
      syncSheetName: config.syncSheetName,
      totalsSheetName: config.totalsSheetName,
      archiveMissingItems: config.archiveMissingItems,
      hasToken: !!config.token
    });
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
    var syncSheet = ensureSheet_(spreadsheet, config.syncSheetName, LEAD_CONTROL_SYNC_HEADERS);
    var towerSheet = ensureSheet_(spreadsheet, config.sheetName, LEAD_CONTROL_HEADERS);
    var totalsSheet = ensureSheet_(spreadsheet, config.totalsSheetName, LEAD_TOTAL_HEADERS);
    var snapshotGeneratedAt = normalizeIsoString_(body.generatedAtUtc) || new Date().toISOString();
    var items = Array.isArray(body.items) ? body.items : [];

    upsertLeadControlSyncRows_(syncSheet, items, snapshotGeneratedAt, config);
    upsertLeadControlRows_(towerSheet, items, snapshotGeneratedAt, config);
    upsertTotalsRows_(totalsSheet, body.totals || {}, body.sources || [], snapshotGeneratedAt);
    syncSheet.hideSheet();
    towerSheet.hideColumns(1);

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
  var syncSheet = ensureSheet_(spreadsheet, config.syncSheetName, LEAD_CONTROL_SYNC_HEADERS);
  var towerSheet = ensureSheet_(spreadsheet, config.sheetName, LEAD_CONTROL_HEADERS);
  ensureSheet_(spreadsheet, config.totalsSheetName, LEAD_TOTAL_HEADERS);
  syncSheet.hideSheet();
  towerSheet.hideColumns(1);
}

function getLeadControlConfig_() {
  var properties = PropertiesService.getScriptProperties();
  return {
    sheetName: properties.getProperty('LEAD_CONTROL_TOWER_SHEET_NAME') || 'Lead Control Tower',
    syncSheetName: properties.getProperty('LEAD_CONTROL_SYNC_SHEET_NAME') || 'Lead Control Sync',
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

  var existingHeaders = sheet.getLastRow() > 0
    ? sheet.getRange(1, 1, 1, Math.max(headers.length, sheet.getLastColumn())).getValues()[0]
    : [];

  var shouldResetHeaders = headers.some(function (header, index) {
    return existingHeaders[index] !== header;
  }) || existingHeaders.length < headers.length;

  if (shouldResetHeaders) {
    sheet.getRange(1, 1, 1, headers.length).setValues([headers]);
    sheet.getRange(1, 1, 1, headers.length).setFontWeight('bold');
  }

  if (sheet.getFrozenRows() !== 1) {
    sheet.setFrozenRows(1);
  }

  return sheet;
}

function upsertLeadControlSyncRows_(sheet, items, snapshotGeneratedAt, config) {
  var headerIndex = buildHeaderIndex_(LEAD_CONTROL_SYNC_HEADERS);
  var existingRows = getExistingDataRows_(sheet, LEAD_CONTROL_SYNC_HEADERS.length);
  var existingRowMap = {};

  existingRows.forEach(function (row, offsetIndex) {
    var key = sanitizeCell_(row[headerIndex.record_key]);
    if (!key) {
      return;
    }

    existingRowMap[key] = {
      rowNumber: offsetIndex + 2,
      values: row
    };
  });

  items.forEach(function (item) {
    var row = buildLeadControlSyncRow_(item, snapshotGeneratedAt, config);
    var key = sanitizeCell_(row[headerIndex.record_key]);
    if (!key) {
      return;
    }

    if (existingRowMap[key]) {
      sheet.getRange(existingRowMap[key].rowNumber, 1, 1, LEAD_CONTROL_SYNC_HEADERS.length).setValues([row]);
      delete existingRowMap[key];
      return;
    }

    sheet.appendRow(row);
  });

  if (config.archiveMissingItems) {
    archiveMissingRows_(sheet, existingRowMap, headerIndex, snapshotGeneratedAt, {
      snapshotStatus: 'snapshot_status',
      lastSeenAt: 'last_seen_at'
    });
  }

  autosizeSheet_(sheet, LEAD_CONTROL_SYNC_HEADERS.length);
}

function buildLeadControlSyncRow_(item, snapshotGeneratedAt, config) {
  var routePath = sanitizeCell_(item.routePath);
  var routeUrl = routePath && config.appBaseUrl
    ? config.appBaseUrl + routePath
    : routePath;

  return [
    sanitizeCell_(item.recordKey),
    sanitizeCell_(item.leadId),
    sanitizeCell_(item.leadName),
    sanitizeCell_(item.location),
    sanitizeCell_(item.category),
    sanitizeCell_(item.source),
    sanitizeCell_(item.sourceReference),
    sanitizeCell_(item.unifiedStatus),
    sanitizeCell_(item.ownerAgentUserId),
    sanitizeCell_(item.ownerAgentName),
    sanitizeCell_(item.ownerResolution),
    sanitizeCell_(item.assignmentStatus),
    toBooleanString_(item.hasBeenContacted),
    normalizeIsoString_(item.firstContactedAt),
    sanitizeCell_(item.contactStatus),
    normalizeIsoString_(item.lastContactedAt),
    sanitizeCell_(item.nextAction),
    normalizeIsoString_(item.nextActionDueAt),
    normalizeIsoString_(item.nextFollowUpAt),
    normalizeIsoString_(item.slaDueAt),
    sanitizeCell_(item.priority),
    sanitizeCell_(normalizeAttentionReasons_(item.attentionReasons)),
    sanitizeCell_(item.openLeadActionCount),
    toBooleanString_(item.hasProspect),
    sanitizeCell_(item.prospectLeadId),
    sanitizeCell_(item.activeCampaignId),
    sanitizeCell_(item.wonCampaignId),
    toBooleanString_(item.convertedToSale),
    sanitizeCell_(item.lastOutcome),
    routePath,
    routeUrl,
    snapshotGeneratedAt,
    'active',
    snapshotGeneratedAt
  ];
}

function upsertLeadControlRows_(sheet, items, snapshotGeneratedAt, config) {
  var headerIndex = buildHeaderIndex_(LEAD_CONTROL_HEADERS);
  var existingRows = getExistingDataRows_(sheet, LEAD_CONTROL_HEADERS.length);
  var existingRowMap = {};

  existingRows.forEach(function (row, offsetIndex) {
    var key = sanitizeCell_(row[headerIndex.record_key]);
    if (!key) {
      return;
    }

    existingRowMap[key] = {
      rowNumber: offsetIndex + 2,
      values: row
    };
  });

  items.forEach(function (item) {
    var existingNotes = '';
    var existingRow = existingRowMap[sanitizeCell_(item.recordKey)];
    if (existingRow) {
      existingNotes = sanitizeCell_(existingRow.values[headerIndex.notes]);
    }

    var row = buildHumanLeadControlRow_(item, snapshotGeneratedAt, config, existingNotes);
    var key = sanitizeCell_(row[headerIndex.record_key]);
    if (!key) {
      return;
    }

    if (existingRowMap[key]) {
      sheet.getRange(existingRowMap[key].rowNumber, 1, 1, LEAD_CONTROL_HEADERS.length).setValues([row]);
      delete existingRowMap[key];
      return;
    }

    sheet.appendRow(row);
  });

  if (config.archiveMissingItems) {
    archiveMissingRows_(sheet, existingRowMap, headerIndex, snapshotGeneratedAt, {
      lastUpdatedAt: 'last_updated_at',
      priority: 'priority',
      attentionReasons: 'attention_reasons'
    });
  }

  autosizeSheet_(sheet, LEAD_CONTROL_HEADERS.length);
}

function buildHumanLeadControlRow_(item, snapshotGeneratedAt, config, existingNotes) {
  var routePath = sanitizeCell_(item.routePath);
  var routeUrl = routePath && config.appBaseUrl
    ? config.appBaseUrl + routePath
    : routePath;

  return [
    sanitizeCell_(item.recordKey),
    sanitizeCell_(item.leadName),
    sanitizeCell_(item.location),
    sanitizeCell_(item.category),
    titleizeValue_(item.source),
    sanitizeCell_(item.ownerAgentName) || resolveOwnerLabel_(item.ownerResolution, item.assignmentStatus),
    titleizeValue_(item.assignmentStatus),
    titleizeValue_(item.unifiedStatus),
    titleizeValue_(item.contactStatus),
    sanitizeCell_(item.nextAction),
    normalizeIsoString_(item.nextActionDueAt),
    normalizeIsoString_(item.nextFollowUpAt),
    normalizeIsoString_(item.slaDueAt),
    titleizeValue_(item.priority),
    titleizeValue_(normalizeAttentionReasons_(item.attentionReasons)),
    sanitizeCell_(item.lastOutcome),
    sanitizeCell_(existingNotes),
    routeUrl ? '=HYPERLINK("' + escapeFormulaText_(routeUrl) + '","Open")' : '',
    snapshotGeneratedAt
  ];
}

function archiveMissingRows_(sheet, missingRowMap, headerIndex, snapshotGeneratedAt, options) {
  Object.keys(missingRowMap).forEach(function (key) {
    var rowInfo = missingRowMap[key];
    var row = rowInfo.values.slice();

    if (options.snapshotStatus && headerIndex[options.snapshotStatus] != null) {
      row[headerIndex[options.snapshotStatus]] = 'missing_from_snapshot';
    }

    if (options.lastSeenAt && headerIndex[options.lastSeenAt] != null) {
      row[headerIndex[options.lastSeenAt]] = snapshotGeneratedAt;
    }

    if (options.lastUpdatedAt && headerIndex[options.lastUpdatedAt] != null) {
      row[headerIndex[options.lastUpdatedAt]] = snapshotGeneratedAt;
    }

    if (options.priority && headerIndex[options.priority] != null) {
      row[headerIndex[options.priority]] = 'Archived';
    }

    if (options.attentionReasons && headerIndex[options.attentionReasons] != null) {
      var currentReasons = sanitizeCell_(row[headerIndex[options.attentionReasons]]);
      row[headerIndex[options.attentionReasons]] = currentReasons
        ? currentReasons + ' | No longer active'
        : 'No longer active';
    }

    sheet.getRange(rowInfo.rowNumber, 1, 1, row.length).setValues([row]);
  });
}

function upsertTotalsRows_(sheet, totals, sources, snapshotGeneratedAt) {
  var headerIndex = buildHeaderIndex_(LEAD_TOTAL_HEADERS);
  var existingRows = getExistingDataRows_(sheet, LEAD_TOTAL_HEADERS.length);
  var existingRowMap = {};

  existingRows.forEach(function (row, offsetIndex) {
    var key = sanitizeCell_(row[headerIndex.metric]);
    if (key) {
      existingRowMap[key] = offsetIndex + 2;
    }
  });

  var topSource = Array.isArray(sources) && sources.length > 0 ? sources[0] : null;
  var metrics = [
    { metric: 'total_leads', value: totals.totalLeadCount },
    { metric: 'owned_leads', value: totals.ownedLeadCount },
    { metric: 'unowned_leads', value: totals.unownedLeadCount },
    { metric: 'ambiguous_owner_leads', value: totals.ambiguousOwnerCount },
    { metric: 'uncontacted_leads', value: totals.uncontactedLeadCount },
    { metric: 'leads_with_next_action', value: totals.leadsWithNextActionCount },
    { metric: 'prospect_leads', value: totals.prospectLeadCount },
    { metric: 'active_deals', value: totals.activeDealCount },
    { metric: 'won_leads', value: totals.wonLeadCount },
    { metric: 'lead_to_prospect_rate_percent', value: totals.leadToProspectRatePercent },
    { metric: 'lead_to_sale_rate_percent', value: totals.leadToSaleRatePercent },
    { metric: 'top_source', value: topSource ? titleizeValue_(topSource.source) : '' }
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

function normalizeAttentionReasons_(value) {
  if (Array.isArray(value)) {
    return value.map(function (entry) {
      return sanitizeCell_(entry);
    }).filter(function (entry) {
      return !!entry;
    }).join(' | ');
  }

  return sanitizeCell_(value);
}

function resolveOwnerLabel_(ownerResolution, assignmentStatus) {
  if (sanitizeCell_(ownerResolution) === 'multiple_action_owners') {
    return 'Multiple owners';
  }

  if (sanitizeCell_(assignmentStatus) === 'unassigned') {
    return 'Unassigned';
  }

  return '';
}

function titleizeValue_(value) {
  return sanitizeCell_(value)
    .split('|')
    .map(function (entry) {
      return entry
        .trim()
        .replace(/_/g, ' ')
        .replace(/\b\w/g, function (character) {
          return character.toUpperCase();
        });
    })
    .filter(function (entry) {
      return !!entry;
    })
    .join(' | ');
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

function escapeFormulaText_(value) {
  return sanitizeCell_(value).replace(/"/g, '""');
}

function jsonResponse_(payload) {
  return ContentService
    .createTextOutput(JSON.stringify(payload))
    .setMimeType(ContentService.MimeType.JSON);
}
