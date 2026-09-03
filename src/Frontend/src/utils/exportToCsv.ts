/**
 * Utility to export an array of objects to a downloadable CSV file.
 */
export function exportToCsv<T extends Record<string, any>>(
  data: T[],
  filename: string,
  headers?: { key: keyof T; label: string }[]
): void {
  if (!data || data.length === 0) {
    alert('No data available to export.');
    return;
  }

  const columns = headers || Object.keys(data[0]).map(key => ({ key, label: key }));

  const csvRows: string[] = [];

  // Header row
  const headerRow = columns.map(col => `"${String(col.label).replace(/"/g, '""')}"`).join(',');
  csvRows.push(headerRow);

  // Data rows
  for (const item of data) {
    const values = columns.map(col => {
      const val = item[col.key];
      if (val === null || val === undefined) return '""';
      if (typeof val === 'object') return `"${JSON.stringify(val).replace(/"/g, '""')}"`;
      return `"${String(val).replace(/"/g, '""')}"`;
    });
    csvRows.push(values.join(','));
  }

  const csvContent = '\uFEFF' + csvRows.join('\n'); // Add UTF-8 BOM for Excel compatibility
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.setAttribute('href', url);
  link.setAttribute('download', filename.endsWith('.csv') ? filename : `${filename}.csv`);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
