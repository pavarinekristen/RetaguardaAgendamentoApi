(function () {
  const config = window.LAUDOS_CONFIG || {};
  const apiBaseUrl = (config.API_BASE_URL || "").replace(/\/+$/, "");

  const state = {
    token: localStorage.getItem("laudos.token") || "",
    usuario: JSON.parse(localStorage.getItem("laudos.usuario") || "null")
  };

  const els = {
    loginView: document.getElementById("loginView"),
    portalView: document.getElementById("portalView"),
    loginForm: document.getElementById("loginForm"),
    loginButton: document.getElementById("loginButton"),
    loginMessage: document.getElementById("loginMessage"),
    email: document.getElementById("email"),
    senha: document.getElementById("senha"),
    logoutButton: document.getElementById("logoutButton"),
    searchForm: document.getElementById("searchForm"),
    searchButton: document.getElementById("searchButton"),
    nome: document.getElementById("nome"),
    clienteId: document.getElementById("clienteId"),
    empresa: document.getElementById("empresa"),
    portalMessage: document.getElementById("portalMessage"),
    results: document.getElementById("results"),
    detailPanel: document.getElementById("detailPanel"),
    closeDetailButton: document.getElementById("closeDetailButton"),
    detailCompany: document.getElementById("detailCompany"),
    detailName: document.getElementById("detailName"),
    detailFields: document.getElementById("detailFields"),
    historyList: document.getElementById("historyList")
  };

  function init() {
    els.loginForm.addEventListener("submit", onLogin);
    els.logoutButton.addEventListener("click", logout);
    els.searchForm.addEventListener("submit", onSearch);
    els.closeDetailButton.addEventListener("click", closeDetail);

    if (state.token) {
      showPortal();
      searchClientes();
    } else {
      showLogin();
    }
  }

  async function onLogin(event) {
    event.preventDefault();
    setMessage(els.loginMessage, "");
    setBusy(els.loginButton, true, "Entrando...");

    try {
      const response = await request("/auth/login", {
        method: "POST",
        body: {
          email: els.email.value.trim(),
          senha: els.senha.value
        },
        auth: false
      });

      const token = response.token || response.Token || "";
      if (!token) {
        throw new Error(response.mensagem || response.Mensagem || "Conta pendente ou nao autorizada.");
      }

      state.token = token;
      state.usuario = response.usuario || response.Usuario || null;
      localStorage.setItem("laudos.token", state.token);
      localStorage.setItem("laudos.usuario", JSON.stringify(state.usuario));

      els.senha.value = "";
      showPortal();
      await searchClientes();
    } catch (error) {
      setMessage(els.loginMessage, error.message, true);
    } finally {
      setBusy(els.loginButton, false, "Entrar");
    }
  }

  function logout() {
    state.token = "";
    state.usuario = null;
    localStorage.removeItem("laudos.token");
    localStorage.removeItem("laudos.usuario");
    els.results.innerHTML = "";
    closeDetail();
    showLogin();
  }

  async function onSearch(event) {
    event.preventDefault();
    await searchClientes();
  }

  async function searchClientes() {
    setMessage(els.portalMessage, "");
    closeDetail();
    setBusy(els.searchButton, true, "...");

    try {
      const params = new URLSearchParams();
      params.set("limite", "50");

      if (els.nome.value.trim()) {
        params.set("nome", els.nome.value.trim());
      }

      if (els.clienteId.value.trim()) {
        params.set("id", els.clienteId.value.trim());
      }

      if (els.empresa.value.trim()) {
        params.set("empresa", els.empresa.value.trim());
      }

      const clientes = await request(`/portal/clientes?${params.toString()}`);
      renderResults(Array.isArray(clientes) ? clientes : []);
    } catch (error) {
      if (error.status === 401) {
        logout();
        setMessage(els.loginMessage, "Sessao expirada. Entre novamente.", true);
        return;
      }

      setMessage(els.portalMessage, error.message, true);
    } finally {
      setBusy(els.searchButton, false, "Buscar");
    }
  }

  function renderResults(clientes) {
    els.results.innerHTML = "";

    if (clientes.length === 0) {
      setMessage(els.portalMessage, "Nenhum cliente encontrado.");
      return;
    }

    setMessage(els.portalMessage, `${clientes.length} cliente(s) encontrado(s).`);

    for (const cliente of clientes) {
      const card = document.createElement("button");
      card.type = "button";
      card.className = "result-card";
      card.addEventListener("click", () => loadDetail(cliente.idLocal || cliente.IdLocal));

      const nome = document.createElement("strong");
      nome.textContent = cliente.nome || cliente.Nome || "Sem nome";

      const meta = document.createElement("span");
      meta.className = "result-meta";
      meta.append(
        line([formatId(cliente.idCadastro || cliente.IdCadastro)]),
        line([cliente.empresa || cliente.Empresa, cliente.cargo || cliente.Cargo]),
        line([cliente.cpf || cliente.Cpf, cliente.rg || cliente.Rg]),
        line([cliente.email || cliente.Email])
      );

      card.append(nome, meta);
      els.results.append(card);
    }
  }

  async function loadDetail(idLocal) {
    if (!idLocal) {
      return;
    }

    setMessage(els.portalMessage, "Carregando cadastro...");

    try {
      const cliente = await request(`/portal/clientes/${encodeURIComponent(idLocal)}`);
      renderDetail(cliente);
      setMessage(els.portalMessage, "");
    } catch (error) {
      if (error.status === 401) {
        logout();
        setMessage(els.loginMessage, "Sessao expirada. Entre novamente.", true);
        return;
      }

      setMessage(els.portalMessage, error.message, true);
    }
  }

  function renderDetail(cliente) {
    els.detailPanel.classList.remove("is-hidden");
    els.detailCompany.textContent = value(cliente, "empresa") || "Cadastro";
    els.detailName.textContent = value(cliente, "nome") || "Sem nome";

    const fields = [
      ["ID", value(cliente, "idCadastro")],
      ["CPF", value(cliente, "cpf")],
      ["RG", value(cliente, "rg")],
      ["Cargo", value(cliente, "cargo")],
      ["E-mail", value(cliente, "email")],
      ["Telefone", value(cliente, "telefone")],
      ["Sexo", value(cliente, "sexo")],
      ["Escolaridade", value(cliente, "escolaridade")],
      ["Estado civil", value(cliente, "estadoCivil")],
      ["Naturalidade", value(cliente, "naturalidade")],
      ["Endereco", value(cliente, "endereco")],
      ["Tipo endereco", value(cliente, "tipoEndereco")],
      ["Observacoes", value(cliente, "observacoes")]
    ];

    els.detailFields.innerHTML = "";
    for (const [label, content] of fields) {
      if (!content) {
        continue;
      }

      const item = document.createElement("div");
      const dt = document.createElement("dt");
      const dd = document.createElement("dd");
      dt.textContent = label;
      dd.textContent = content;
      item.append(dt, dd);
      els.detailFields.append(item);
    }

    renderHistory(cliente.historico || cliente.Historico || []);
    els.detailPanel.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function renderHistory(items) {
    els.historyList.innerHTML = "";

    if (!items.length) {
      const empty = document.createElement("p");
      empty.className = "message";
      empty.textContent = "Sem historico sincronizado.";
      els.historyList.append(empty);
      return;
    }

    for (const item of items) {
      const node = document.createElement("article");
      node.className = "history-item";

      const title = document.createElement("strong");
      title.textContent = line([
        value(item, "data"),
        value(item, "horario"),
        value(item, "motivo")
      ]).textContent || "Atendimento";

      const meta = document.createElement("span");
      meta.textContent = line([
        value(item, "status"),
        value(item, "tipoLaudo"),
        value(item, "empresa")
      ]).textContent;

      const obs = document.createElement("span");
      obs.textContent = value(item, "observacao");

      node.append(title, meta);
      if (obs.textContent) {
        node.append(obs);
      }

      els.historyList.append(node);
    }
  }

  function closeDetail() {
    els.detailPanel.classList.add("is-hidden");
    els.detailFields.innerHTML = "";
    els.historyList.innerHTML = "";
  }

  async function request(path, options) {
    const opts = options || {};
    const headers = {
      "Accept": "application/json"
    };

    if (opts.body) {
      headers["Content-Type"] = "application/json";
    }

    if (opts.auth !== false && state.token) {
      headers["Authorization"] = `Bearer ${state.token}`;
    }

    const response = await fetch(`${apiBaseUrl}${path}`, {
      method: opts.method || "GET",
      headers,
      body: opts.body ? JSON.stringify(opts.body) : undefined
    });

    const contentType = response.headers.get("content-type") || "";
    const payload = contentType.includes("application/json")
      ? await response.json()
      : null;

    if (!response.ok) {
      const error = new Error(errorMessage(payload, response.status));
      error.status = response.status;
      throw error;
    }

    return payload;
  }

  function errorMessage(payload, status) {
    if (payload) {
      return payload.mensagem || payload.Mensagem || payload.message || payload.Message || "Erro ao processar solicitacao.";
    }

    return status === 0 ? "API indisponivel." : `Erro ${status}.`;
  }

  function showLogin() {
    els.loginView.classList.remove("is-hidden");
    els.portalView.classList.add("is-hidden");
  }

  function showPortal() {
    els.loginView.classList.add("is-hidden");
    els.portalView.classList.remove("is-hidden");
  }

  function setBusy(button, busy, text) {
    button.disabled = busy;
    button.textContent = text;
  }

  function setMessage(element, text, isError) {
    element.textContent = text || "";
    element.classList.toggle("is-error", Boolean(isError));
  }

  function value(obj, key) {
    if (!obj) {
      return "";
    }

    const pascal = key.charAt(0).toUpperCase() + key.slice(1);
    return obj[key] || obj[pascal] || "";
  }

  function line(parts) {
    const span = document.createElement("span");
    span.textContent = parts.filter(Boolean).join(" - ");
    return span;
  }

  function formatId(id) {
    return id ? `ID ${id}` : "";
  }

  init();
})();
