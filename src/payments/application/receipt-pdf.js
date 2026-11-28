/**
 * Comprobante de pago de una cuota en PDF (A4). jsPDF se carga solo al generar el comprobante
 * para no sumar peso a la carga inicial de la aplicación.
 *
 * data = {
 *   labels: { title, number, issuedAt, paid, condominium, unit, holder, aliquot, period, breakdown, concept, amount,
 *             total, payment, paidAt, method, reference, footer },
 *   number, issuedAt, condominium, unit, holder, aliquot, period,
 *   lines: [{ concept, amount }], total, paidAt, method, reference
 * }
 * Los montos llegan ya formateados (formatPen) para que coincidan con lo que ve el residente en pantalla.
 */
const GREEN = [16, 185, 129];
const DARK = [17, 24, 39];
const MUTED = [107, 114, 128];
const LINE = [229, 231, 235];

async function loadLogo() {
    try {
        const blob = await (await fetch('/IoBuild-Logo.png')).blob();
        const dataUrl = await new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = reject;
            reader.readAsDataURL(blob);
        });
        const size = await new Promise((resolve) => {
            const img = new Image();
            img.onload = () => resolve({ w: img.naturalWidth, h: img.naturalHeight });
            img.onerror = () => resolve(null);
            img.src = dataUrl;
        });
        return size ? { dataUrl, ...size } : null;
    } catch (_) {
        return null;   // sin logo el comprobante sigue siendo válido
    }
}

export async function buildReceiptPdf(data) {
    const [{ jsPDF }, logo] = await Promise.all([import('jspdf'), loadLogo()]);
    const doc = new jsPDF({ unit: 'mm', format: 'a4' });
    const L = data.labels;
    const left = 20, right = 190, width = right - left;
    let y = 20;

    // Encabezado: logo a la izquierda, título y número a la derecha
    if (logo) {
        const h = 12, w = Math.min(50, (logo.w / logo.h) * h);
        doc.addImage(logo.dataUrl, 'PNG', left, y - 2, w, h);
    } else {
        doc.setFont('helvetica', 'bold').setFontSize(18).setTextColor(...DARK).text('IoBuild', left, y + 7);
    }
    doc.setFont('helvetica', 'bold').setFontSize(16).setTextColor(...DARK).text(L.title, right, y + 2, { align: 'right' });
    doc.setFont('helvetica', 'normal').setFontSize(9).setTextColor(...MUTED);
    doc.text(`${L.number}: ${data.number}`, right, y + 8, { align: 'right' });
    doc.text(`${L.issuedAt}: ${data.issuedAt}`, right, y + 13, { align: 'right' });

    y += 20;
    doc.setDrawColor(...GREEN).setLineWidth(0.8).line(left, y, right, y);

    // Estado: pagado
    y += 10;
    doc.setFillColor(236, 253, 245).setDrawColor(...GREEN).setLineWidth(0.3).roundedRect(left, y - 5.5, 28, 8, 2, 2, 'FD');
    doc.setFont('helvetica', 'bold').setFontSize(9).setTextColor(6, 95, 70).text(L.paid.toUpperCase(), left + 14, y, { align: 'center' });

    // Datos de la unidad en dos columnas
    y += 12;
    const facts = [
        [L.condominium, data.condominium], [L.unit, data.unit],
        [L.holder, data.holder], [L.aliquot, data.aliquot],
        [L.period, data.period],
    ];
    facts.forEach(([label, value], i) => {
        const x = i % 2 === 0 ? left : left + width / 2;
        const rowY = y + Math.floor(i / 2) * 13;
        doc.setFont('helvetica', 'normal').setFontSize(8.5).setTextColor(...MUTED).text(label, x, rowY);
        doc.setFont('helvetica', 'bold').setFontSize(11).setTextColor(...DARK).text(String(value ?? '—'), x, rowY + 5.5);
    });
    y += Math.ceil(facts.length / 2) * 13 + 6;

    // Desglose de la cuota
    doc.setFont('helvetica', 'bold').setFontSize(12).setTextColor(...DARK).text(L.breakdown, left, y);
    y += 5;
    doc.setFillColor(249, 250, 251).rect(left, y, width, 8, 'F');
    doc.setFont('helvetica', 'bold').setFontSize(9).setTextColor(...MUTED);
    doc.text(L.concept, left + 3, y + 5.5);
    doc.text(L.amount, right - 3, y + 5.5, { align: 'right' });
    y += 8;
    doc.setFont('helvetica', 'normal').setFontSize(10.5).setTextColor(...DARK);
    data.lines.forEach((line) => {
        y += 8;
        doc.text(line.concept, left + 3, y);
        doc.text(line.amount, right - 3, y, { align: 'right' });
        doc.setDrawColor(...LINE).setLineWidth(0.2).line(left, y + 3, right, y + 3);
    });
    y += 11;
    doc.setFont('helvetica', 'bold').setFontSize(12).text(L.total, left + 3, y);
    doc.setTextColor(...GREEN).text(data.total, right - 3, y, { align: 'right' });

    // Datos del pago
    y += 14;
    doc.setFont('helvetica', 'bold').setFontSize(12).setTextColor(...DARK).text(L.payment, left, y);
    y += 3;
    [[L.paidAt, data.paidAt], [L.method, data.method], [L.reference, data.reference]].forEach(([label, value]) => {
        y += 7;
        doc.setFont('helvetica', 'normal').setFontSize(10).setTextColor(...MUTED).text(label, left + 3, y);
        // La referencia de Stripe es larga: se ajusta al ancho disponible
        const text = doc.setTextColor(...DARK).splitTextToSize(String(value || '—'), width - 55);
        doc.text(text, left + 50, y);
        y += (text.length - 1) * 4.5;
    });

    // Pie
    doc.setDrawColor(...LINE).setLineWidth(0.3).line(left, 272, right, 272);
    doc.setFont('helvetica', 'normal').setFontSize(8).setTextColor(...MUTED);
    doc.text(doc.splitTextToSize(L.footer, width), left, 277);

    return doc;
}
