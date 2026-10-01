// Formatea la coleccion de Postman con 2 espacios, LF y salto final (formato de Postman/Node).
// PowerShell 5.1 (ConvertTo-Json) usa otra indentacion y escapa '&', '<', '>' y '\''; esto evita diffs gigantes.
// Uso: node scripts/format-postman.js
const fs = require('fs');
const path = require('path');

const file = path.resolve(__dirname, '..', 'postman', 'Danec.PdfGenerator.postman_collection.json');
const json = JSON.parse(fs.readFileSync(file, 'utf8').replace(/^﻿/, ''));
fs.writeFileSync(file, JSON.stringify(json, null, 2) + '\n', 'utf8');
console.log('Formateado:', file);
