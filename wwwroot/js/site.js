// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener('click', (event) => {
    document.querySelectorAll('.nav-dropdown.open').forEach((dropdown) => {
        if (!dropdown.contains(event.target)) {
            dropdown.classList.remove('open');
        }
    });
});

function formatCurrency(value) {
    return Number(value).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

function formatDate(value) {
    return value ? new Date(value).toLocaleDateString('pt-BR', { timeZone: 'UTC' }) : '—';
}

function formatValorFinalCell(item) {
    const desconto = item.percentualDesconto > 0
        ? `<br><small class="muted">-${item.percentualDesconto.toLocaleString('pt-BR')}% de ${formatCurrency(item.valorTotal)}</small>`
        : '';
    return `${formatCurrency(item.valorComDesconto)}${desconto}`;
}

function formatStatusBadge(status) {
    const classes = {
        'Concluída': 'status-badge--concluida',
        'Pendente': 'status-badge--pendente',
        'Cancelada': 'status-badge--cancelada',
        'Devolução': 'status-badge--devolucao'
    };
    const classe = classes[status] || 'status-badge--pendente';
    return `<span class="status-badge ${classe}">${status}</span>`;
}

function formatVencimentoPagamentoCell(item) {
    if (item.dataPagamento) {
        const venc = item.dataVencimento ? `<br><small class="muted">Venc.: ${formatDate(item.dataVencimento)}</small>` : '';
        return `Pago em ${formatDate(item.dataPagamento)}${venc}`;
    }
    if (item.dataVencimento) {
        return `Venc.: ${formatDate(item.dataVencimento)}<br><small class="muted">Em aberto</small>`;
    }
    return '—';
}
