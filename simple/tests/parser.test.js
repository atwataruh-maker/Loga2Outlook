/**
 * Abhängigkeitsfreie Tests für die reine Parser-/ICS-Logik aus
 * ../loga-urlaub-export.js (kein Browser, kein DOM, kein npm-Paket nötig).
 *
 * Aufruf:  node tests/parser.test.js
 */
"use strict";

const assert = require("assert");
const path = require("path");

const {
  assignYearsAndBuildEntries,
  formatIcsDate,
  buildIcs,
  buildUid,
} = require(path.join(__dirname, "..", "loga-urlaub-export.js"));

const STATUSES = ["Genehmigt", "Genommen"];

function row(von, bis, status) {
  return { kind: "row", von: von, bis: bis, status: status };
}

function year(value) {
  return { kind: "year", text: String(value) };
}

let passed = 0;
let failed = 0;

function test(name, fn) {
  try {
    fn();
    passed++;
    console.log("OK   " + name);
  } catch (err) {
    failed++;
    console.error("FAIL " + name);
    console.error("     " + err.message);
  }
}

// ---------------------------------------------------------------------------
// Vom Auftrag geforderte Testfälle
// ---------------------------------------------------------------------------

test("eintägiger Urlaub", () => {
  const items = [year(2026), row("23.07.", "23.07.", "Genehmigt")];
  const { entries, warnings } = assignYearsAndBuildEntries(items, STATUSES);

  assert.strictEqual(warnings.length, 0);
  assert.strictEqual(entries.length, 1);
  assert.strictEqual(formatIcsDate(entries[0].start), "20260723");
  assert.strictEqual(formatIcsDate(entries[0].end), "20260723");
});

test("mehrtägiger Urlaub", () => {
  const items = [year(2026), row("23.07.", "24.07.", "Genehmigt")];
  const { entries } = assignYearsAndBuildEntries(items, STATUSES);

  assert.strictEqual(entries.length, 1);
  assert.strictEqual(formatIcsDate(entries[0].start), "20260723");
  assert.strictEqual(formatIcsDate(entries[0].end), "20260724");
});

test("mehrere Einträge im selben Jahr", () => {
  const items = [
    year(2026),
    row("05.02.", "06.02.", "Genommen"),
    row("07.04.", "10.04.", "Genommen"),
    row("23.07.", "24.07.", "Genehmigt"),
    row("03.08.", "14.08.", "Genehmigt"),
  ];
  const { entries, warnings } = assignYearsAndBuildEntries(items, STATUSES);

  assert.strictEqual(warnings.length, 0);
  assert.strictEqual(entries.length, 4);
});

test("Status Genommen und Genehmigt werden übernommen, andere Status nicht", () => {
  const items = [
    year(2026),
    row("05.02.", "06.02.", "Genommen"),
    row("10.02.", "11.02.", "Beantragt"),
    row("23.07.", "24.07.", "Genehmigt"),
    row("01.09.", "02.09.", "Abgelehnt"),
    row("05.09.", "06.09.", "Storniert"),
  ];
  const { entries } = assignYearsAndBuildEntries(items, STATUSES);

  assert.strictEqual(entries.length, 2);
  assert.strictEqual(entries[0].status, "Genommen");
  assert.strictEqual(entries[1].status, "Genehmigt");
});

test("Einträge in zwei verschiedenen Jahren werden korrekt zugeordnet", () => {
  const items = [
    year(2026),
    row("23.07.", "24.07.", "Genehmigt"),
    row("03.08.", "14.08.", "Genehmigt"),
    year(2027),
    row("05.01.", "06.01.", "Genehmigt"),
  ];
  const { entries } = assignYearsAndBuildEntries(items, STATUSES);

  assert.strictEqual(entries.length, 3);
  assert.strictEqual(entries[0].start.getFullYear(), 2026);
  assert.strictEqual(entries[1].start.getFullYear(), 2026);
  assert.strictEqual(entries[2].start.getFullYear(), 2027);
});

test("Urlaub über den Jahreswechsel hinweg (Ende liegt vor Start)", () => {
  const items = [year(2026), row("29.12.", "02.01.", "Genehmigt")];
  const { entries } = assignYearsAndBuildEntries(items, STATUSES);

  assert.strictEqual(entries.length, 1);
  assert.strictEqual(formatIcsDate(entries[0].start), "20261229");
  assert.strictEqual(formatIcsDate(entries[0].end), "20270102");
});

test("leere Tabelle liefert keine Einträge und keine Warnung", () => {
  const { entries, warnings } = assignYearsAndBuildEntries([], STATUSES);

  assert.strictEqual(entries.length, 0);
  assert.strictEqual(warnings.length, 0);
});

// ---------------------------------------------------------------------------
// Zusätzliche Robustheitstests
// ---------------------------------------------------------------------------

test("Zeile ohne vorheriges Jahr erzeugt eine verständliche Warnung und wird übersprungen", () => {
  const items = [row("23.07.", "24.07.", "Genehmigt")];
  const { entries, warnings } = assignYearsAndBuildEntries(items, STATUSES);

  assert.strictEqual(entries.length, 0);
  assert.strictEqual(warnings.length, 1);
  assert.ok(warnings[0].indexOf("Kein Jahr") !== -1);
});

test("nicht parsebares Datum erzeugt eine Warnung statt eines falschen Eintrags", () => {
  const items = [year(2026), row("ungueltig", "24.07.", "Genehmigt")];
  const { entries, warnings } = assignYearsAndBuildEntries(items, STATUSES);

  assert.strictEqual(entries.length, 0);
  assert.strictEqual(warnings.length, 1);
});

test("stabiles, deterministisches UID-Schema anhand Start-/Enddatum", () => {
  const items = [year(2026), row("23.07.", "24.07.", "Genehmigt")];
  const { entries } = assignYearsAndBuildEntries(items, STATUSES);
  const uid = buildUid(entries[0].start, entries[0].end);

  assert.strictEqual(uid, "loga-urlaub-20260723-20260724@loga-urlaub-export");

  // Erneuter Aufruf mit denselben Daten muss dieselbe UID liefern.
  const items2 = [year(2026), row("23.07.", "24.07.", "Genehmigt")];
  const { entries: entries2 } = assignYearsAndBuildEntries(items2, STATUSES);
  assert.strictEqual(buildUid(entries2[0].start, entries2[0].end), uid);
});

test("ICS: exklusives Enddatum (Enddatum + 1 Tag), CLASS:PRIVATE, OOF, keine Erinnerung", () => {
  const items = [year(2026), row("23.07.", "24.07.", "Genehmigt")];
  const { entries } = assignYearsAndBuildEntries(items, STATUSES);
  const ics = buildIcs(entries);

  assert.ok(ics.includes("BEGIN:VCALENDAR"));
  assert.ok(ics.includes("SUMMARY:Urlaub"));
  assert.ok(ics.includes("DTSTART;VALUE=DATE:20260723"));
  assert.ok(ics.includes("DTEND;VALUE=DATE:20260725")); // 24.07. + 1 Tag exklusiv
  assert.ok(ics.includes("CLASS:PRIVATE"));
  assert.ok(ics.includes("X-MICROSOFT-CDO-BUSYSTATUS:OOF"));
  assert.ok(!ics.includes("BEGIN:VALARM"));
});

test("ICS: mehrere Einträge erzeugen mehrere VEVENT-Blöcke mit passenden UIDs", () => {
  const items = [
    year(2026),
    row("05.02.", "06.02.", "Genommen"),
    row("23.07.", "24.07.", "Genehmigt"),
  ];
  const { entries } = assignYearsAndBuildEntries(items, STATUSES);
  const ics = buildIcs(entries);

  const vEventCount = (ics.match(/BEGIN:VEVENT/g) || []).length;
  assert.strictEqual(vEventCount, 2);
  assert.ok(ics.includes("UID:loga-urlaub-20260205-20260206@loga-urlaub-export"));
  assert.ok(ics.includes("UID:loga-urlaub-20260723-20260724@loga-urlaub-export"));
});

test("leere Eintragsliste erzeugt ein gültiges, leeres Kalendergerüst", () => {
  const ics = buildIcs([]);
  assert.ok(ics.includes("BEGIN:VCALENDAR"));
  assert.ok(ics.includes("END:VCALENDAR"));
  assert.ok(!ics.includes("BEGIN:VEVENT"));
});

// ---------------------------------------------------------------------------
console.log("\n" + passed + " von " + (passed + failed) + " Test(s) erfolgreich.");
if (failed > 0) {
  console.error(failed + " Test(s) fehlgeschlagen.");
  process.exitCode = 1;
}
