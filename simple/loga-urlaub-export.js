/**
 * LOGA Urlaubsexport
 *
 * Liest die LOGA-"Urlaubsübersicht"-Tabelle direkt aus dem DOM der bereits
 * angemeldeten LOGA-Seite und erzeugt daraus eine ICS-Datei ("LOGA-Urlaub.ics")
 * mit allen genehmigten und genommenen Urlaubszeiträumen als ganztägige,
 * "Abwesend" markierte Termine.
 *
 * Rein clientseitig:
 *   - keine Zugangsdaten werden abgefragt oder gespeichert (das Skript nutzt
 *     ausschließlich die bereits im Browser angemeldete Sitzung),
 *   - kein Server, keine Übertragung von Daten nach außen,
 *   - kein Edge WebDriver, kein Selenium, kein PowerShell-Modul, kein PHP.
 *
 * Verwendung: als Lesezeichen ("Bookmarklet") ablegen (siehe README.md) und auf
 * der geöffneten, bereits sichtbaren Urlaubsübersicht anklicken. Diese Datei ist
 * die gut lesbare Entwicklungs-/Wartungsversion; das eigentliche Lesezeichen
 * enthält denselben Code in einer kompakten javascript:-URI
 * (siehe loga-urlaub-export.bookmarklet.txt).
 *
 * Voraussetzung im DOM: die Urlaubsübersicht muss bereits geöffnet und im
 * Dropdown auf "Urlaub" eingestellt sein (siehe README.md).
 */
(function () {
  "use strict";

  /** Einzige Domain, auf der dieses Skript ausgeführt werden darf. */
  var ALLOWED_HOSTNAME = "dedalus.pi-asp.de";

  /** Nur diese LOGA-Status werden als Outlook-Termine übernommen. */
  var ALLOWED_STATUSES = ["Genehmigt", "Genommen"];

  // ==========================================================================
  // Sicherheits-/Umgebungsprüfung
  // ==========================================================================

  /**
   * Bricht mit einer verständlichen Meldung ab, wenn das Skript nicht auf der
   * konfigurierten LOGA-Domain ausgeführt wird.
   */
  function assertAllowedDomain() {
    if (window.location.hostname !== ALLOWED_HOSTNAME) {
      var message =
        "LOGA-Urlaubsexport: Dieses Lesezeichen darf nur auf " +
        ALLOWED_HOSTNAME +
        " ausgeführt werden.\nAktuelle Seite: " +
        window.location.hostname;
      window.alert(message);
      throw new Error(message);
    }
  }

  // ==========================================================================
  // DOM-Extraktion (nur im Browser lauffähig, nicht per Node testbar)
  //
  // Läuft einmal in Dokumentreihenfolge über den Baum und erzeugt eine flache
  // Liste von "Signalen": Jahresmarkierungen und Datenzeilen. Verwendet
  // ausschließlich die bestätigten, stabilen Klassen
  // ".LG-InputLabel.Cell.Von" / ".Cell.Bis" / ".Cell.Text" - NICHT die
  // dynamisch vergebenen id="LGLabel203"-Attribute, die sich bei jedem
  // Rendern ändern können.
  //
  // Die Jahreszuordnung selbst wird nicht hier, sondern in der reinen Funktion
  // assignYearsAndBuildEntries() vorgenommen, damit diese ohne echten Browser-
  // DOM getestet werden kann.
  // ==========================================================================

  /**
   * Prüft, ob ein Element aktuell sichtbar gerendert ist (um versteckte oder
   * im DOM verbliebene alte Popups nicht versehentlich mit auszuwerten).
   */
  function isVisible(element) {
    return !!(element.offsetWidth || element.offsetHeight || element.getClientRects().length);
  }

  /** Prüft, ob ein Element eine der drei bestätigten Zellen-Klassen trägt. */
  function hasCellClass(element, name) {
    return (
      !!element.classList &&
      element.classList.contains("LG-InputLabel") &&
      element.classList.contains("Cell") &&
      element.classList.contains(name)
    );
  }

  /**
   * Erkennt eine "nackte" Jahreszahl (z. B. "2026") in einem Blatt-Element ohne
   * Kindelemente. Diese Heuristik ist bewusst konservativ (reiner 4-stelliger
   * Text, kein Kindknoten), um nicht versehentlich einen umschließenden
   * Container mit vielen anderen Zahlen als "Jahr" misszuverstehen.
   */
  function isYearLabel(element) {
    if (element.children.length !== 0) return false;
    if (hasCellClass(element, "Von") || hasCellClass(element, "Bis") || hasCellClass(element, "Text")) {
      return false;
    }
    return /^20\d{2}$/.test(element.textContent.trim());
  }

  function closestRow(element) {
    return typeof element.closest === "function" ? element.closest("tr") : null;
  }

  /**
   * Liefert die in Dokumentreihenfolge sortierte Signal-Liste, die
   * assignYearsAndBuildEntries() als Eingabe erwartet.
   */
  function extractDomOrderItems(root) {
    var items = [];
    var seenRows = new Set();

    var walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null);
    var node = walker.currentNode;

    while (node) {
      if (isVisible(node)) {
        if (isYearLabel(node)) {
          items.push({ kind: "year", text: node.textContent.trim() });
        } else if (hasCellClass(node, "Von")) {
          var row = closestRow(node);
          if (row && !seenRows.has(row)) {
            seenRows.add(row);
            var bisElement = row.querySelector(".LG-InputLabel.Cell.Bis");
            var textElement = row.querySelector(".LG-InputLabel.Cell.Text");
            items.push({
              kind: "row",
              von: node.textContent.trim(),
              bis: bisElement ? bisElement.textContent.trim() : "",
              status: textElement ? textElement.textContent.trim() : "",
            });
          }
        }
      }
      node = walker.nextNode();
    }

    return items;
  }

  // ==========================================================================
  // Reine Verarbeitungslogik (kein DOM-Zugriff, per Node testbar - siehe
  // tests/parser.test.js)
  // ==========================================================================

  /**
   * Parst ein LOGA-Kurzdatum wie "05.02." zusammen mit einem bekannten Jahr in
   * ein Date-Objekt (lokale Zeitzone, ohne Uhrzeitanteil). Gibt null zurück,
   * wenn der Text nicht dem erwarteten Format entspricht.
   */
  function parseGermanShortDate(text, year) {
    var match = /^(\d{1,2})\.(\d{1,2})\.?$/.exec((text || "").trim());
    if (!match) return null;

    var day = parseInt(match[1], 10);
    var month = parseInt(match[2], 10);
    if (month < 1 || month > 12 || day < 1 || day > 31) return null;

    return new Date(year, month - 1, day);
  }

  /**
   * Ordnet jeder Datenzeile das zuletzt zuvor in der Signal-Liste gesehene
   * Jahr zu, filtert auf die zu übernehmenden Status, parst die Daten und
   * behandelt Zeiträume, die über einen Jahreswechsel hinweg gehen (Enddatum
   * liegt kalendarisch vor dem Startdatum -> Enddatum gehört zum Folgejahr).
   *
   * @param {Array} domOrderItems Signale in Dokumentreihenfolge, siehe
   *   extractDomOrderItems() bzw. die Testfälle in tests/parser.test.js.
   * @param {string[]} allowedStatuses Zu übernehmende Status-Texte.
   * @returns {{entries: Array, warnings: string[]}}
   */
  function assignYearsAndBuildEntries(domOrderItems, allowedStatuses) {
    var entries = [];
    var warnings = [];
    var currentYear = null;

    (domOrderItems || []).forEach(function (item, index) {
      if (item.kind === "year") {
        currentYear = parseInt(item.text, 10);
        return;
      }

      if (item.kind !== "row") return;

      if (allowedStatuses.indexOf(item.status) === -1) {
        // Andere Status (z. B. "Beantragt", "Abgelehnt") werden bewusst ignoriert.
        return;
      }

      if (currentYear === null) {
        warnings.push(
          "Kein Jahr für Zeitraum '" + item.von + " - " + item.bis + "' gefunden (Position " + index + ")."
        );
        return;
      }

      var start = parseGermanShortDate(item.von, currentYear);
      var end = parseGermanShortDate(item.bis, currentYear);

      if (!start || !end) {
        warnings.push("Datum konnte nicht geparst werden: '" + item.von + "' - '" + item.bis + "'.");
        return;
      }

      if (end < start) {
        // Zeitraum ueber den Jahreswechsel hinweg (z. B. 29.12. - 02.01.).
        end = new Date(end.getFullYear() + 1, end.getMonth(), end.getDate());
      }

      entries.push({ start: start, end: end, status: item.status });
    });

    return { entries: entries, warnings: warnings };
  }

  function pad(number) {
    return (number < 10 ? "0" : "") + number;
  }

  /** Formatiert ein Datum als ICS-Datumswert (YYYYMMDD), in lokaler Zeit. */
  function formatIcsDate(date) {
    return "" + date.getFullYear() + pad(date.getMonth() + 1) + pad(date.getDate());
  }

  /** Formatiert einen Zeitpunkt als ICS-UTC-Zeitstempel (fuer DTSTAMP). */
  function formatIcsTimestamp(date) {
    return (
      date.getUTCFullYear() +
      pad(date.getUTCMonth() + 1) +
      pad(date.getUTCDate()) +
      "T" +
      pad(date.getUTCHours()) +
      pad(date.getUTCMinutes()) +
      pad(date.getUTCSeconds()) +
      "Z"
    );
  }

  /**
   * Stabiles, deterministisches UID-Schema aus Start- und Enddatum, damit ein
   * erneuter Export desselben Zeitraums dieselbe UID erzeugt.
   */
  function buildUid(start, end) {
    return "loga-urlaub-" + formatIcsDate(start) + "-" + formatIcsDate(end) + "@loga-urlaub-export";
  }

  /**
   * Baut den vollständigen ICS-Dateiinhalt aus den erkannten Einträgen.
   * Jeder Termin: ganztägig, Betreff "Urlaub", CLASS:PRIVATE, Outlook-Status
   * "Abwesend" (X-MICROSOFT-CDO-BUSYSTATUS:OOF), keine Erinnerung (kein
   * VALARM-Block), exklusives Enddatum (Enddatum + 1 Tag).
   */
  function buildIcs(entries) {
    var lines = ["BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//LOGA Urlaub Export//DE", "CALSCALE:GREGORIAN"];

    var now = new Date();

    entries.forEach(function (entry) {
      var exclusiveEnd = new Date(entry.end.getFullYear(), entry.end.getMonth(), entry.end.getDate() + 1);

      lines.push("BEGIN:VEVENT");
      lines.push("UID:" + buildUid(entry.start, entry.end));
      lines.push("DTSTAMP:" + formatIcsTimestamp(now));
      lines.push("DTSTART;VALUE=DATE:" + formatIcsDate(entry.start));
      lines.push("DTEND;VALUE=DATE:" + formatIcsDate(exclusiveEnd));
      lines.push("SUMMARY:Urlaub");
      lines.push("CLASS:PRIVATE");
      lines.push("TRANSP:OPAQUE");
      lines.push("X-MICROSOFT-CDO-BUSYSTATUS:OOF");
      lines.push("X-MICROSOFT-CDO-ALLDAYEVENT:TRUE");
      lines.push("END:VEVENT");
    });

    lines.push("END:VCALENDAR");
    return lines.join("\r\n");
  }

  /** Formatiert ein Datum fuer die Vorschau-Anzeige (TT.MM.JJJJ). */
  function formatGermanDate(date) {
    return pad(date.getDate()) + "." + pad(date.getMonth() + 1) + "." + date.getFullYear();
  }

  // ==========================================================================
  // Datei-Download (nur im Browser)
  // ==========================================================================

  function downloadIcs(content, filename) {
    var blob = new Blob([content], { type: "text/calendar;charset=utf-8" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.setTimeout(function () {
      URL.revokeObjectURL(url);
    }, 10000);
  }

  // ==========================================================================
  // Ablauf
  // ==========================================================================

  function run() {
    assertAllowedDomain();

    var domOrderItems = extractDomOrderItems(document.body);
    var result = assignYearsAndBuildEntries(domOrderItems, ALLOWED_STATUSES);

    if (result.entries.length === 0) {
      var noEntriesMessage =
        "LOGA-Urlaubsexport: Es wurden keine Urlaubszeiträume erkannt.\n\n" +
        "Bitte prüfen, ob die Urlaubsübersicht geöffnet ist und im Dropdown " +
        "\"Urlaub\" ausgewählt wurde (siehe README.md).";
      if (result.warnings.length > 0) {
        noEntriesMessage += "\n\nHinweise:\n" + result.warnings.join("\n");
      }
      window.alert(noEntriesMessage);
      return;
    }

    var preview = result.entries
      .map(function (entry) {
        return formatGermanDate(entry.start) + " - " + formatGermanDate(entry.end) + " (" + entry.status + ")";
      })
      .join("\n");

    var confirmMessage =
      "LOGA-Urlaubsexport: " + result.entries.length + " Urlaubszeitraum/-zeiträume gefunden:\n\n" + preview;
    if (result.warnings.length > 0) {
      confirmMessage += "\n\nWarnungen (bitte prüfen):\n" + result.warnings.join("\n");
    }
    confirmMessage += "\n\nJetzt LOGA-Urlaub.ics herunterladen?";

    if (!window.confirm(confirmMessage)) {
      return;
    }

    downloadIcs(buildIcs(result.entries), "LOGA-Urlaub.ics");
  }

  // In Node (Tests) wird nur exportiert, im Browser wird direkt ausgeführt.
  if (typeof module !== "undefined" && module.exports) {
    module.exports = {
      assignYearsAndBuildEntries: assignYearsAndBuildEntries,
      parseGermanShortDate: parseGermanShortDate,
      buildIcs: buildIcs,
      formatIcsDate: formatIcsDate,
      buildUid: buildUid,
    };
  } else {
    run();
  }
})();
