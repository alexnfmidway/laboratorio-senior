/**
 * ===================================================================
 * ESCOLA DE MICROSSERVIÇOS - LÓGICA INTERATIVA (APP.JS)
 * ===================================================================
 */

document.addEventListener('DOMContentLoaded', () => {
  initMermaid();
  initTheme();
  initScrollSpyAndProgress();
  initSearch();
  initJaegerSimulator();
  initCircuitBreakerSimulator();
  initQuiz();
  initCopyButtons();
  initChecklist();
});

/* ===================================================================
   1. INICIALIZAÇÃO DO MERMAID.JS COM SUPORTE A TEMA
   =================================================================== */
function initMermaid() {
  if (window.mermaid) {
    const isDark = document.body.classList.contains('dark-theme');
    mermaid.initialize({
      startOnLoad: true,
      theme: isDark ? 'dark' : 'neutral',
      fontFamily: 'Plus Jakarta Sans',
      securityLevel: 'loose',
    });
  }
}

/* ===================================================================
   2. CONTROLE DE TEMA (CLARO / ESCURO)
   =================================================================== */
function initTheme() {
  const toggleBtn = document.getElementById('theme-toggle');
  const themeIcon = document.getElementById('theme-icon');

  // Recupera preferência salva (padrão claro / leve)
  const savedTheme = localStorage.getItem('app-theme') || 'light';
  applyTheme(savedTheme);

  toggleBtn.addEventListener('click', () => {
    const currentTheme = document.body.classList.contains('dark-theme') ? 'dark' : 'light';
    const nextTheme = currentTheme === 'dark' ? 'light' : 'dark';
    applyTheme(nextTheme);
    localStorage.setItem('app-theme', nextTheme);
  });

  function applyTheme(theme) {
    if (theme === 'dark') {
      document.body.classList.remove('light-theme');
      document.body.classList.add('dark-theme');
      themeIcon.textContent = '☀️';
      toggleBtn.setAttribute('title', 'Alternar para Modo Claro');
    } else {
      document.body.classList.remove('dark-theme');
      document.body.classList.add('light-theme');
      themeIcon.textContent = '🌙';
      toggleBtn.setAttribute('title', 'Alternar para Modo Escuro');
    }
  }
}

/* ===================================================================
   3. SCROLL SPY & BARRA DE PROGRESSO DO ESTUDANTE
   =================================================================== */
function initScrollSpyAndProgress() {
  const navItems = document.querySelectorAll('.sidebar-nav .nav-item');
  const sections = document.querySelectorAll('main section');
  const progressBar = document.getElementById('progress-bar-fill');
  const progressPercentText = document.getElementById('progress-percent');

  // Atualiza barra de progresso no scroll da página
  window.addEventListener('scroll', () => {
    const scrollTop = window.scrollY;
    const docHeight = document.documentElement.scrollHeight - window.innerHeight;
    const progress = Math.min(100, Math.max(0, Math.round((scrollTop / docHeight) * 100)));

    if (progressBar) progressBar.style.width = `${progress}%`;
    if (progressPercentText) progressPercentText.textContent = `${progress}%`;

    // Atualiza item ativo da sidebar
    let currentSectionId = '';
    sections.forEach((section) => {
      const rect = section.getBoundingClientRect();
      if (rect.top <= 200 && rect.bottom >= 100) {
        currentSectionId = section.id;
      }
    });

    if (currentSectionId) {
      navItems.forEach((item) => {
        item.classList.toggle('active', item.getAttribute('data-target') === currentSectionId);
      });
    }
  });
}

/* ===================================================================
   4. BUSCADOR INTELIGENTE DE CONCEITOS
   =================================================================== */
function initSearch() {
  const searchInput = document.getElementById('module-search');
  const sections = document.querySelectorAll('.lesson-module');

  searchInput.addEventListener('input', (e) => {
    const query = e.target.value.toLowerCase().trim();

    if (!query) {
      sections.forEach((sec) => (sec.style.display = 'block'));
      return;
    }

    sections.forEach((sec) => {
      const text = sec.innerText.toLowerCase();
      if (text.includes(query)) {
        sec.style.display = 'block';
      } else {
        sec.style.display = 'none';
      }
    });
  });
}

/* ===================================================================
   5. SIMULADOR INTERATIVO DO JAEGER TRACING
   =================================================================== */
function initJaegerSimulator() {
  const btnRun = document.getElementById('btn-run-trace');
  const traceStatus = document.getElementById('trace-status');
  const traceIdEl = document.getElementById('current-trace-id');
  const durationEl = document.getElementById('current-trace-duration');
  const spanCountEl = document.getElementById('current-span-count');
  const tagsBox = document.getElementById('span-tags-box');

  const barGateway = document.getElementById('bar-gateway');
  const barOrder = document.getElementById('bar-order');
  const barInv = document.getElementById('bar-inv');
  const barPay = document.getElementById('bar-pay');

  let isRunning = false;

  btnRun.addEventListener('click', () => {
    if (isRunning) return;
    isRunning = true;
    btnRun.disabled = true;

    // Reseta visual
    if (barGateway) {
      barGateway.style.width = '0%';
      barGateway.textContent = 'Iniciando Gateway...';
    }
    barOrder.style.width = '0%';
    barInv.style.width = '0%';
    barPay.style.width = '0%';
    barOrder.textContent = 'Aguardando proxy...';
    barInv.textContent = '0 ms';
    barPay.textContent = '0 ms';

    // Gera Trace ID no padrão W3C hex (32 caracteres)
    const traceId = Array.from({ length: 32 }, () => Math.floor(Math.random() * 16).toString(16)).join('');
    traceIdEl.textContent = traceId;
    traceStatus.textContent = '🚪 GatewayApi (porta 5000) recebeu o pedido e gerou o Trace raiz...';
    spanCountEl.textContent = '1 / 4';

    // Etapa 1: Inicia GatewayApi (YARP na porta 5000)
    if (barGateway) {
      barGateway.style.width = '100%';
      barGateway.textContent = 'GatewayApi (YARP Proxy)';
    }

    // Etapa 2: YARP roteia para OrderApi na rede interna (porta 8080)
    setTimeout(() => {
      traceStatus.textContent = '🛒 YARP reencaminhou para OrderApi:8080 (rede privada)...';
      spanCountEl.textContent = '2 / 4';
      barOrder.style.width = '96%';
      barOrder.style.left = '2%';
      barOrder.textContent = 'Executando OrderApi...';

      // Etapa 3: OrderApi descobre e chama InventoryApi via Docker DNS
      setTimeout(() => {
        traceStatus.textContent = '📦 Docker DNS resolveu "inventoryapi" ➜ Estoque verificado (~98ms)...';
        spanCountEl.textContent = '3 / 4';
        barInv.style.width = '37%';
        barInv.style.left = '6%';
        barInv.textContent = '98.4 ms (Estoque)';

        // Etapa 4: OrderApi descobre e chama PaymentApi via Docker DNS
        setTimeout(() => {
          traceStatus.textContent = '💳 Docker DNS resolveu "paymentapi" ➜ Cartão cobrado (~151ms)...';
          spanCountEl.textContent = '4 / 4';
          barPay.style.width = '55%';
          barPay.style.left = '43%';
          barPay.textContent = '151.2 ms (Pagamento)';

          // Etapa 5: Finaliza trace completo com sucesso
          setTimeout(() => {
            durationEl.textContent = '258.4 ms';
            if (barGateway) barGateway.textContent = '258.4 ms (Gateway Raiz - Porta 5000)';
            barOrder.textContent = '254.6 ms (OrderApi interna)';
            traceStatus.textContent = '✅ Trace distribuído completo correlacionado no Jaeger!';
            btnRun.disabled = false;
            isRunning = false;

            tagsBox.innerHTML = `
              <div style="display: flex; flex-direction: column; gap: 0.35rem;">
                <div><strong>🔍 Detalhes da Cadeia Distribuída Completa (4 Microsserviços):</strong></div>
                <div>• <code>traceparent: 00-${traceId}-01</code> (propagado automaticamente pelo YARP e HttpClient).</div>
                <div>• <strong>Span 1 (Raiz):</strong> GatewayApi (258.4ms) | <code>http.method=POST /order</code> | Porta: <code>5000</code></div>
                <div>• <strong>Span 2 (Filho):</strong> OrderApi (254.6ms) | <code>http.target=orderapi:8080</code> (privado)</div>
                <div>• <strong>Span 3 (Neto):</strong> InventoryApi (98.4ms) | Descoberto via: <code>Docker DNS</code></div>
                <div>• <strong>Span 4 (Neto):</strong> PaymentApi (151.2ms) | Descoberto via: <code>Docker DNS</code></div>
                <div style="color: var(--ms-green-dark); font-weight: 600; margin-top: 0.2rem;">
                  🎯 Conclusão: O cliente só conversou com o Gateway na porta 5000, e o Jaeger rastreou os 4 nós perfeitamente!
                </div>
              </div>
            `;
          }, 500);
        }, 500);
      }, 400);
    }, 350);
  });
}

/* ===================================================================
   6. SIMULADOR DO CIRCUIT BREAKER COM POLLY
   =================================================================== */
function initCircuitBreakerSimulator() {
  const bulb = document.getElementById('cb-bulb');
  const stateText = document.getElementById('cb-state-text');
  const stateDesc = document.getElementById('cb-state-desc');
  const consecutiveFailsEl = document.getElementById('cb-consecutive-fails');
  const totalBlockedEl = document.getElementById('cb-total-blocked');
  const cooldownEl = document.getElementById('cb-cooldown');
  const toggleCrash = document.getElementById('toggle-payment-crash');
  const btnSend = document.getElementById('btn-cb-send');
  const btnReset = document.getElementById('btn-cb-reset');
  const logList = document.getElementById('cb-log-list');

  let state = 'CLOSED'; // 'CLOSED', 'OPEN', 'HALF_OPEN'
  let consecutiveFails = 0;
  let totalBlocked = 0;
  let cooldownSeconds = 0;
  let cooldownInterval = null;

  function addLog(message, type = 'info') {
    const time = new Date().toLocaleTimeString();
    const entry = document.createElement('div');
    entry.className = `log-entry ${type}`;
    entry.textContent = `[${time}] ${message}`;
    logList.insertBefore(entry, logList.firstChild);
    if (logList.children.length > 20) {
      logList.removeChild(logList.lastChild);
    }
  }

  function updateUI() {
    consecutiveFailsEl.textContent = `${consecutiveFails} / 3`;
    totalBlockedEl.textContent = totalBlocked;
    cooldownEl.textContent = `${cooldownSeconds}s`;

    if (state === 'CLOSED') {
      bulb.textContent = '🟢';
      stateText.textContent = 'FECHADO (Closed)';
      stateText.style.color = 'var(--accent-emerald)';
      stateDesc.textContent = 'Tudo saudável. Requisições passam livremente.';
    } else if (state === 'OPEN') {
      bulb.textContent = '🔴';
      stateText.textContent = 'ABERTO (Open)';
      stateText.style.color = 'var(--accent-rose)';
      stateDesc.textContent = 'Disjuntor desarmado! Requisições bloqueadas em 0ms.';
    } else if (state === 'HALF_OPEN') {
      bulb.textContent = '🟡';
      stateText.textContent = 'MEIO-ABERTO (Half-Open)';
      stateText.style.color = 'var(--accent-amber)';
      stateDesc.textContent = 'Testando 1 requisição para verificar recuperação.';
    }
  }

  function tripCircuitBreaker() {
    state = 'OPEN';
    cooldownSeconds = 8; // 8 segundos para a demo interativa
    addLog('⚡ ALERTA: 3 falhas consecutivas! O Disjuntor DESARMOU (Estado: ABERTO)!', 'error');
    addLog('🛡️ Polly agora protegerá o sistema rejeitando chamadas de imediato.', 'warning');
    updateUI();

    if (cooldownInterval) clearInterval(cooldownInterval);
    cooldownInterval = setInterval(() => {
      cooldownSeconds--;
      cooldownEl.textContent = `${cooldownSeconds}s`;

      if (cooldownSeconds <= 0) {
        clearInterval(cooldownInterval);
        state = 'HALF_OPEN';
        addLog('🟡 Tempo de espera finalizado. Disjuntor mudou para MEIO-ABERTO (Half-Open).', 'warning');
        addLog('👉 Próxima requisição será um teste para ver se o Payment voltou.', 'info');
        updateUI();
      }
    }, 1000);
  }

  btnSend.addEventListener('click', () => {
    const isCrashed = toggleCrash.checked;

    if (state === 'OPEN') {
      totalBlocked++;
      addLog(`⛔ BLOQUEADO INSTANTANEAMENTE (0ms)! Disjuntor ABERTO. Economizou conexões e memória.`, 'warning');
      updateUI();
      return;
    }

    if (state === 'HALF_OPEN') {
      if (isCrashed) {
        addLog('❌ Falha na requisição de teste! O serviço PaymentApi ainda está fora do ar.', 'error');
        tripCircuitBreaker();
      } else {
        state = 'CLOSED';
        consecutiveFails = 0;
        addLog('✅ Requisição de teste teve SUCESSO! PaymentApi recuperada.', 'success');
        addLog('🟢 Disjuntor foi REARMADO para FECHADO (Closed). Fluxo normal restabelecido!', 'success');
        updateUI();
      }
      return;
    }

    // Estado CLOSED
    if (isCrashed) {
      consecutiveFails++;
      addLog(`❌ Falha na chamada HTTP para PaymentApi (HTTP 500 Internal Error) [${consecutiveFails}/3].`, 'error');
      if (consecutiveFails >= 3) {
        tripCircuitBreaker();
      } else {
        updateUI();
      }
    } else {
      consecutiveFails = 0;
      addLog('✅ Requisição processada com sucesso! OrderApi ➜ InventoryApi ➜ PaymentApi (200 OK).', 'success');
      updateUI();
    }
  });

  btnReset.addEventListener('click', () => {
    if (cooldownInterval) clearInterval(cooldownInterval);
    state = 'CLOSED';
    consecutiveFails = 0;
    totalBlocked = 0;
    cooldownSeconds = 0;
    toggleCrash.checked = false;
    addLog('🔄 Disjuntor reiniciado manualmente para o estado inicial FECHADO.', 'info');
    updateUI();
  });
}

/* ===================================================================
   7. QUIZ DE FIXAÇÃO COM FEEDBACK DIDÁTICO IMEDIATO
   =================================================================== */
function initQuiz() {
  const cards = document.querySelectorAll('.quiz-question-card');

  const explanations = {
    1: '🎯 Exatamente! O Trace ID é como o código de rastreio de um pacote: ele viaja de API em API via cabeçalho HTTP (traceparent) para que o Jaeger una todas as chamadas em uma linha do tempo única.',
    2: '⚡ Perfeito! Quando o disjuntor está ABERTO, ele "corta a corrente". A requisição nem tenta sair pela rede, falhando na hora em 0ms. Isso evita que a sua API esgote conexões esperando por um serviço que já está caído.',
    3: '📬 Isso aí! O Outbox Pattern grava o Pedido e a Mensagem na mesma transação SQL local (ACID). Dessa forma, é impossível o pedido ser salvo sem a mensagem correspondente existir para ser despachada depois.',
  };

  cards.forEach((card) => {
    const qNum = card.getAttribute('data-q');
    const buttons = card.querySelectorAll('.opt-btn');
    const feedback = card.querySelector('.feedback-msg');

    buttons.forEach((btn) => {
      btn.addEventListener('click', () => {
        const isCorrect = btn.getAttribute('data-correct') === 'true';

        // Reseta classes do grupo
        buttons.forEach((b) => b.classList.remove('correct', 'wrong'));

        if (isCorrect) {
          btn.classList.add('correct');
          feedback.textContent = explanations[qNum];
          feedback.style.color = 'var(--accent-emerald)';
        } else {
          btn.classList.add('wrong');
          feedback.textContent = '❌ Não é bem isso. Tente outra alternativa ou revise o conceito acima!';
          feedback.style.color = 'var(--accent-rose)';
        }
      });
    });
  });
}

/* ===================================================================
   8. BOTÕES DE COPIAR CÓDIGO & COMANDOS
   =================================================================== */
function initCopyButtons() {
  document.addEventListener('click', (e) => {
    if (e.target && e.target.classList.contains('btn-copy')) {
      const btn = e.target;
      const targetSelector = btn.getAttribute('data-clipboard-target');
      let textToCopy = '';

      if (targetSelector) {
        const targetEl = document.querySelector(targetSelector);
        if (targetEl) textToCopy = targetEl.innerText.trim();
      } else {
        const parentBox = btn.closest('.command-box') || btn.closest('.code-spotlight');
        if (parentBox) {
          const codeEl = parentBox.querySelector('.command-text') || parentBox.querySelector('code');
          if (codeEl) textToCopy = codeEl.innerText.trim();
        }
      }

      if (textToCopy) {
        navigator.clipboard.writeText(textToCopy).then(() => {
          const originalText = btn.textContent;
          btn.textContent = '✅ Copiado!';
          btn.style.backgroundColor = 'var(--accent-emerald)';
          btn.style.color = '#ffffff';

          setTimeout(() => {
            btn.textContent = originalText;
            btn.style.backgroundColor = '';
            btn.style.color = '';
          }, 2000);
        });
      }
    }
  });
}

/* ===================================================================
   9. CHECKLIST INTERATIVO DO TUTORIAL PASSO A PASSO
   =================================================================== */
function initChecklist() {
  const checklistItems = document.querySelectorAll('.checklist-item');
  const storageKey = 'microservices-tutorial-checklist';
  let savedState = {};

  try {
    savedState = JSON.parse(localStorage.getItem(storageKey)) || {};
  } catch (err) {
    savedState = {};
  }

  checklistItems.forEach((item, index) => {
    const checkbox = item.querySelector('input[type="checkbox"]');
    if (!checkbox) return;

    // Restaura estado salvo
    if (savedState[index]) {
      checkbox.checked = true;
      item.classList.add('done');
    }

    item.addEventListener('click', (e) => {
      if (e.target !== checkbox) {
        checkbox.checked = !checkbox.checked;
      }
      item.classList.toggle('done', checkbox.checked);
      savedState[index] = checkbox.checked;
      localStorage.setItem(storageKey, JSON.stringify(savedState));
    });
  });
}
