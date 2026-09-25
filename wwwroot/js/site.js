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

function toast(mensagem, tipo = 'success') {
    let container = document.getElementById('app-toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'app-toast-container';
        container.className = 'app-toast-container';
        document.body.appendChild(container);
    }

    const el = document.createElement('div');
    el.className = `app-toast app-toast--${tipo}`;
    el.textContent = mensagem;
    container.appendChild(el);

    setTimeout(() => {
        el.classList.add('app-toast--saindo');
        setTimeout(() => el.remove(), 250);
    }, 3200);
}

function linhaCarregando(colspan) {
    return `<tr><td colspan="${colspan}" class="muted">Carregando...</td></tr>`;
}

function filtrarEPaginar(dados, { termo = '', campos = [], pagina = 1, porPagina = 8 } = {}) {
    const termoNormalizado = (termo || '').trim().toLowerCase();
    const filtrados = termoNormalizado
        ? dados.filter(item => campos.some(campo => String(item[campo] ?? '').toLowerCase().includes(termoNormalizado)))
        : dados.slice();

    const totalPaginas = Math.max(1, Math.ceil(filtrados.length / porPagina));
    const paginaAtual = Math.min(Math.max(1, pagina), totalPaginas);
    const inicio = (paginaAtual - 1) * porPagina;

    return {
        itens: filtrados.slice(inicio, inicio + porPagina),
        totalFiltrados: filtrados.length,
        totalPaginas,
        paginaAtual
    };
}

function renderPaginacao(elemento, paginaAtual, totalPaginas, aoMudarPagina) {
    if (totalPaginas <= 1) {
        elemento.innerHTML = '';
        return;
    }

    elemento.innerHTML = `
        <button type="button" class="table-action" ${paginaAtual <= 1 ? 'disabled' : ''} data-pagina="${paginaAtual - 1}">← Anterior</button>
        <span class="muted" style="margin:0 10px;font-size:12px;">Página ${paginaAtual} de ${totalPaginas}</span>
        <button type="button" class="table-action" ${paginaAtual >= totalPaginas ? 'disabled' : ''} data-pagina="${paginaAtual + 1}">Próxima →</button>
    `;

    elemento.querySelectorAll('[data-pagina]').forEach(botao => {
        botao.addEventListener('click', () => aoMudarPagina(Number(botao.dataset.pagina)));
    });
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
