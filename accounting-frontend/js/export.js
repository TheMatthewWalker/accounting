// Export utility — loaded as a plain script (not ES module) so CDN globals are available.
// Depends on: jsPDF (window.jspdf), jsPDF-AutoTable, SheetJS (window.XLSX)

window.ExportUtils = {
    /**
     * Export to PDF using jsPDF + autoTable.
     * @param {string} title   - Report title printed at top
     * @param {string} subtitle - Optional subtitle / date range
     * @param {string[]} columns - Column header labels
     * @param {Array[]} rows    - Array of row arrays (values as strings)
     * @param {string} filename - Output filename (no extension)
     * @param {object[]} [footerRows] - Optional totals/footer rows (bold)
     */
    toPDF(title, subtitle, columns, rows, filename, footerRows = []) {
        const { jsPDF } = window.jspdf;
        const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });

        // Title block
        doc.setFontSize(16);
        doc.setFont(undefined, 'bold');
        doc.text(title, 14, 18);
        doc.setFont(undefined, 'normal');
        doc.setFontSize(10);
        if (subtitle) doc.text(subtitle, 14, 25);
        doc.text(`Printed: ${new Date().toLocaleDateString('en-GB')}`, 14, subtitle ? 31 : 25);

        const startY = subtitle ? 37 : 31;

        const bodyRows = rows.map(r => r.map(v => v ?? ''));
        const footRows = footerRows.map(r => r.map(v => v ?? ''));

        doc.autoTable({
            head: [columns],
            body: bodyRows,
            foot: footRows,
            startY,
            styles: { fontSize: 8, cellPadding: 2 },
            headStyles: { fillColor: [41, 98, 162], textColor: 255, fontStyle: 'bold' },
            footStyles: { fillColor: [230, 236, 245], fontStyle: 'bold' },
            alternateRowStyles: { fillColor: [248, 249, 250] },
            columnStyles: this._buildColumnStyles(columns)
        });

        doc.save(filename + '.pdf');
    },

    /**
     * Export to Excel using SheetJS.
     * @param {string} title
     * @param {string[]} columns
     * @param {Array[]} rows
     * @param {string} filename
     * @param {object[]} [footerRows]
     */
    toExcel(title, columns, rows, filename, footerRows = []) {
        const wb = XLSX.utils.book_new();
        const wsData = [
            [title],
            [],
            columns,
            ...rows,
            ...(footerRows.length ? [[]] : []),
            ...footerRows
        ];
        const ws = XLSX.utils.aoa_to_sheet(wsData);

        // Style header row (row index 2, 0-based)
        const headerRowIdx = 2;
        columns.forEach((_, ci) => {
            const cellRef = XLSX.utils.encode_cell({ r: headerRowIdx, c: ci });
            if (ws[cellRef]) {
                ws[cellRef].s = { font: { bold: true }, fill: { fgColor: { rgb: '2962A2' } } };
            }
        });

        // Auto column widths
        const maxLens = columns.map((h, ci) => {
            const vals = [h, ...rows.map(r => String(r[ci] ?? ''))];
            return Math.min(40, Math.max(...vals.map(v => String(v).length)));
        });
        ws['!cols'] = maxLens.map(w => ({ wch: w + 2 }));

        XLSX.utils.book_append_sheet(wb, ws, title.substring(0, 31));
        XLSX.writeFile(wb, filename + '.xlsx');
    },

    // Right-align numeric columns (those whose header or content looks numeric)
    _buildColumnStyles(columns) {
        const styles = {};
        columns.forEach((col, i) => {
            if (/debit|credit|balance|amount|total|outstanding|invoiced|received|paid/i.test(col)) {
                styles[i] = { halign: 'right' };
            }
        });
        return styles;
    },

    /** Render export button bar and attach to a container element */
    addButtons(containerId, onPDF, onExcel) {
        const existing = document.getElementById('exportBar');
        if (existing) existing.remove();

        const bar = document.createElement('div');
        bar.id = 'exportBar';
        bar.style.cssText = 'display:flex;gap:10px;margin-top:16px;';

        const pdfBtn = document.createElement('button');
        pdfBtn.className = 'btn btn-secondary btn-sm';
        pdfBtn.textContent = '⬇ Export PDF';
        pdfBtn.onclick = onPDF;

        const xlBtn = document.createElement('button');
        xlBtn.className = 'btn btn-secondary btn-sm';
        xlBtn.textContent = '⬇ Export Excel';
        xlBtn.onclick = onExcel;

        bar.appendChild(pdfBtn);
        bar.appendChild(xlBtn);

        const container = document.getElementById(containerId);
        if (container) container.appendChild(bar);
    }
};
