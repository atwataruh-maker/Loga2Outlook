/**
 * Entwicklungs-Hilfsskript: erzeugt aus der gut lesbaren Quelldatei
 * loga-urlaub-export.js die kompakte javascript:-URI fuer das Lesezeichen
 * (loga-urlaub-export.bookmarklet.txt). Wird von Endbenutzern NICHT benoetigt -
 * die Bookmarklet-Datei liegt bereits fertig generiert im Repository.
 *
 * Aufruf: node build-bookmarklet.js
 */
"use strict";

const fs = require("fs");
const path = require("path");

const sourcePath = path.join(__dirname, "loga-urlaub-export.js");
const outputPath = path.join(__dirname, "loga-urlaub-export.bookmarklet.txt");

let source = fs.readFileSync(sourcePath, "utf8");

// Den Node-spezifischen Export-Zweig durch einen direkten Aufruf ersetzen -
// im Bookmarklet gibt es kein "module", das Skript soll dort immer sofort laufen.
const exportBranchPattern =
  /\/\/ In Node[\s\S]*?\n\s*\} else \{\n\s*run\(\);\n\s*\}/;
if (!exportBranchPattern.test(source)) {
  throw new Error(
    "Der Node-Export-Zweig wurde in loga-urlaub-export.js nicht gefunden - " +
      "build-bookmarklet.js muss an die geaenderte Struktur angepasst werden."
  );
}
source = source.replace(exportBranchPattern, "run();");

// Block- und Zeilenkommentare entfernen. Die Quelldatei enthaelt bewusst keine
// "//" oder "/*" innerhalb von String-Literalen, daher ist dieser einfache
// Ansatz hier sicher (kein vollwertiger JS-Parser noetig).
source = source.replace(/\/\*[\s\S]*?\*\//g, "");
source = source.replace(/^\s*\/\/.*$/gm, "");

// Leerzeilen und Einrueckung entfernen, um die URI-Laenge zu reduzieren.
source = source
  .split("\n")
  .map((line) => line.trim())
  .filter((line) => line.length > 0)
  .join("\n");

const bookmarklet = "javascript:" + encodeURIComponent(source);

fs.writeFileSync(outputPath, bookmarklet, "utf8");
console.log("Bookmarklet geschrieben nach " + outputPath + " (" + bookmarklet.length + " Zeichen).");
