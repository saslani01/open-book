const API_BASE = "";
const MAX_CHATS = 5; // already have late limiter at the backend, so we are fine

let chats = [];
let activeId = null;

const tabsEl = document.getElementById("tabs");
const usernameEl = document.getElementById("username");
const startEl = document.getElementById("start");
const statusEl = document.getElementById("status");
const chatBox = document.getElementById("chat-container");

const activeChat = () => chats.find(c => c.id === activeId);
const avatarUrl = (username, size) => `https://github.com/${encodeURIComponent(username)}.png?size=${size}`;

function newChat() {
  const username = usernameEl.value.trim();
  if (!username) {
    statusEl.textContent = "Type a username first.";
    return;
  }
  if (chats.length >= MAX_CHATS) {
    statusEl.textContent = "Max 5 chats. Remove one first.";
    return;
  }

  startEl.disabled = true;
  statusEl.textContent = "Starting...";
  
  fetch(`${API_BASE}/api/chat/${encodeURIComponent(username)}/start`, { method: "POST" })
    .then((res) => {
      if (!res.ok) {
        throw new Error("Bad Request")
      }
      return res.json();

    })
    .then((session) => {
      chats.push({ id: session.sessionId, username: session.username, history: [] });
      activeId = session.sessionId;
      usernameEl.value = "";
      statusEl.textContent = "";
      render();
    })
    .catch((err) => {
      statusEl.textContent = "Failed: " + err.message
    })
    .finally(() => {
      startEl.disabled = false;
    })
}

function removeChat(id) {
  fetch(`${API_BASE}/api/chat/session/${id}`, { method: "DELETE" })
    .catch(() => {});
  chats = chats.filter(c => c.id !== id);
  
  if (activeId === id) 
    activeId = chats.length ? chats[0].id : null;
  render();
}

async function handler(body, signals) {
  const chat = activeChat();

  const message = body.messages[body.messages.length - 1]?.text || "";
  try {
    const res = await fetch(`${API_BASE}/api/chat/send?sessionId=${encodeURIComponent(chat.id)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message })
    });
    if (res.status === 429) {
      signals.onResponse({ error: "Rate limited (10/min). Wait a bit." });
      return;
    }
    if (!res.ok) {
      signals.onResponse({ error: (await res.json().catch(() => ({}))).error || `HTTP ${res.status}` });
      return;
    }
    const data = await res.json();
    chat.history.push({ role: "user", text: message });
    chat.history.push({ role: "ai", text: data.message });
    signals.onResponse({ text: data.message });
    statusEl.textContent = `${data.tokensUsed} tokens | ${data.contextMode}` + (data.matchedRepository ? ` | ${data.matchedRepository}` : "");
  } catch {
    signals.onResponse({ error: "Network error." });
  }
}

function renderTabs() {
  tabsEl.replaceChildren();
  chats.forEach(chat => {
    const group = document.createElement("div");
    group.className = "btn-group btn-group-sm";

    const open = document.createElement("button");
    open.className = "btn " + (chat.id === activeId ? "btn-primary" : "btn-outline-primary");

    const avatar = document.createElement("img");
    avatar.src = avatarUrl(chat.username, 40);
    avatar.width = 20;
    avatar.height = 20;
    avatar.className = "rounded-circle me-1";
    avatar.alt = "";
    open.append(avatar, document.createTextNode("@" + chat.username));
    open.onclick = () => {
      activeId = chat.id;
      render();
    };

    const close = document.createElement("button");
    close.className = "btn btn-outline-danger";
    close.textContent = "X";
    close.title = "Remove chat";
    close.onclick = () => removeChat(chat.id);

    group.append(open, close);
    tabsEl.append(group);
  });
  startEl.disabled = chats.length >= MAX_CHATS;
}

function renderChat() {
  chatBox.replaceChildren();
  const el = document.createElement("deep-chat");
  el.className = "w-100 h-100";
  el.connect = { handler };

  const chat = activeChat();
  if (chat) {
    el.history = chat.history;
    el.avatars = { ai: { src: avatarUrl(chat.username, 80) } };
    el.introMessage = { text: `Chatting with @${chat.username}.` };
  } 
  
  else {
    el.introMessage = { text: "Create a chat above to begin." };
    el.textInput = { disabled: true, placeholder: { text: "Start a chat to begin" } };
  }
  chatBox.append(el);
}

function render() {
  renderTabs();
  renderChat();
}

startEl.addEventListener("click", newChat);
usernameEl.addEventListener("keydown", e => {
  if (e.key === "Enter") newChat();
});

await customElements.whenDefined("deep-chat");
render();