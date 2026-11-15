(() => {
    const root = document.querySelector('[data-support-staff]');
    if (!root) return;

    const token = () => document.querySelector('#support-antiforgery input[name="__RequestVerificationToken"]').value;
    const escapeHtml = value => String(value).replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]);
    const list = document.getElementById('support-conversation-list');
    const messagesHost = document.getElementById('support-thread-messages');
    const errorHost = document.getElementById('support-error');
    const presenceButton = document.getElementById('staff-presence-toggle');
    let online = false;
    let selectedId = null;
    let afterMessageId = 0;
    let inboxTimer;
    let presenceTimer;
    let messagesLoading = false;

    const showError = message => {
        errorHost.textContent = message;
        errorHost.hidden = false;
    };
    const call = async (url, options = {}) => {
        const response = await fetch(url, {
            ...options,
            headers: {
                ...(options.body ? { 'Content-Type': 'application/json' } : {}),
                ...(options.method ? { 'RequestVerificationToken': token() } : {}),
                ...options.headers
            }
        });
        const result = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(result.error || `Yêu cầu thất bại (HTTP ${response.status}).`);
        return result;
    };
    const updatePresenceUi = () => {
        presenceButton.setAttribute('aria-pressed', String(online));
        presenceButton.textContent = online ? 'Tắt trực tuyến' : 'Bật trực tuyến';
        document.getElementById('staff-presence-state').textContent = online ? 'Đang trực tuyến' : 'Đang ngoại tuyến';
    };
    const setPresence = async value => {
        await call('/api/support-chat/availability', { method: 'POST', body: JSON.stringify({ online: value }) });
        online = value;
        updatePresenceUi();
    };
    const renderInbox = conversations => {
        if (!conversations.length) {
            list.innerHTML = '<p class="support-empty">Chưa có hội thoại đang chờ.</p>';
            return;
        }
        list.innerHTML = conversations.map(conversation => `
            <button type="button" class="support-conversation ${conversation.id === selectedId ? 'is-selected' : ''}" data-conversation-id="${conversation.id}">
                <span class="support-conversation-top"><strong>${escapeHtml(conversation.customerName)}</strong><span>${conversation.status === 'Waiting' ? 'ĐANG CHỜ' : 'ĐANG CHAT'}</span></span>
                <small>${conversation.messageCount} tin nhắn · ${conversation.assignedEmployeeName ? `Phụ trách: ${escapeHtml(conversation.assignedEmployeeName)}` : 'Chưa có người nhận'}</small>
            </button>`).join('');
    };
    const appendMessage = message => {
        const bubble = document.createElement('article');
        bubble.className = `support-message ${message.senderRole === 'Customer' ? 'from-customer' : 'from-staff'}`;
        bubble.innerHTML = `<strong>${escapeHtml(message.senderName)}</strong><p>${escapeHtml(message.content)}</p><time>${new Date(message.createdAtUtc).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}</time>`;
        messagesHost.append(bubble);
        messagesHost.scrollTop = messagesHost.scrollHeight;
    };
    const loadMessages = async initial => {
        if (!selectedId || messagesLoading) return;
        messagesLoading = true;
        const conversationId = selectedId;
        try {
            const result = await call(`/api/support-chat/${conversationId}/messages?after=${initial ? 0 : afterMessageId}`);
            if (selectedId !== conversationId) return;
            const messages = result.messages || [];
            if (initial) {
                messagesHost.replaceChildren();
                afterMessageId = 0;
            }
            for (const message of messages) {
                afterMessageId = Math.max(afterMessageId, message.id);
                appendMessage(message);
            }
            if (result.status === 'Closed') {
                document.getElementById('support-thread-state').textContent = 'Đã kết thúc';
                document.getElementById('support-reply-form').hidden = true;
                document.getElementById('support-close').hidden = true;
            }
        } finally {
            messagesLoading = false;
        }
    };
    const selectConversation = async conversation => {
        selectedId = conversation.id;
        afterMessageId = 0;
        messagesHost.replaceChildren();
        document.getElementById('support-thread-title').textContent = conversation.customerName;
        document.getElementById('support-thread-state').textContent = conversation.status === 'Waiting' ? 'Đang chờ nhân viên nhận' : `Đang trò chuyện${conversation.assignedEmployeeName ? ` · ${conversation.assignedEmployeeName}` : ''}`;
        const canClaim = conversation.status === 'Waiting';
        const active = conversation.status === 'Active';
        document.getElementById('support-claim').hidden = !canClaim;
        document.getElementById('support-close').hidden = !active;
        document.getElementById('support-reply-form').hidden = !active;
        renderInbox(window.supportConversations || []);
        if (canClaim) {
            messagesHost.innerHTML = '<p class="support-empty">Nhận hội thoại để xem nội dung và bắt đầu tư vấn.</p>';
            return;
        }
        try {
            await loadMessages(true);
        } catch (error) {
            showError(error.message);
        }
    };
    const refreshInbox = async () => {
        try {
            const result = await call('/api/support-chat/inbox');
            window.supportConversations = result.conversations || [];
            renderInbox(window.supportConversations);
            const selected = window.supportConversations.find(item => item.id === selectedId);
            if (!selected && selectedId !== null) {
                selectedId = null;
                afterMessageId = 0;
                document.getElementById('support-thread-title').textContent = 'Chọn một hội thoại';
                document.getElementById('support-thread-state').textContent = '';
                document.getElementById('support-claim').hidden = true;
                document.getElementById('support-close').hidden = true;
                document.getElementById('support-reply-form').hidden = true;
                messagesHost.innerHTML = '<p class="support-empty">Hội thoại đã kết thúc hoặc được nhân viên khác nhận.</p>';
            } else if (selected) {
                await loadMessages(false);
            }
            errorHost.hidden = true;
        } catch (error) {
            showError(error.message);
        }
    };

    const restorePresence = async () => {
        try {
            const result = await call('/api/support-chat/availability');
            online = result.online;
            updatePresenceUi();
            if (online) presenceTimer = setInterval(() => setPresence(true).catch(error => showError(error.message)), 25000);
        } catch (error) {
            showError(error.message);
        }
    };
    presenceButton.addEventListener('click', async () => {
        presenceButton.disabled = true;
        try {
            await setPresence(!online);
            clearInterval(presenceTimer);
            if (online) presenceTimer = setInterval(() => setPresence(true).catch(error => showError(error.message)), 25000);
        } catch (error) {
            showError(error.message);
        } finally {
            presenceButton.disabled = false;
        }
    });
    list.addEventListener('click', event => {
        const button = event.target.closest('[data-conversation-id]');
        if (!button) return;
        const conversation = (window.supportConversations || []).find(item => item.id === Number(button.dataset.conversationId));
        if (conversation) selectConversation(conversation);
    });
    document.getElementById('support-claim').addEventListener('click', async event => {
        event.currentTarget.disabled = true;
        try {
            const result = await call(`/api/support-chat/${selectedId}/claim`, { method: 'POST', body: '{}' });
            await refreshInbox();
            await selectConversation(result.conversation);
        } catch (error) {
            showError(error.message);
        } finally {
            event.currentTarget.disabled = false;
        }
    });
    document.getElementById('support-close').addEventListener('click', async event => {
        event.currentTarget.disabled = true;
        try {
            await call(`/api/support-chat/${selectedId}/close`, { method: 'POST', body: '{}' });
            document.getElementById('support-thread-state').textContent = 'Đã kết thúc';
            document.getElementById('support-reply-form').hidden = true;
            document.getElementById('support-close').hidden = true;
            await refreshInbox();
        } catch (error) {
            showError(error.message);
        } finally {
            event.currentTarget.disabled = false;
        }
    });
    document.getElementById('support-reply-form').addEventListener('submit', async event => {
        event.preventDefault();
        const input = document.getElementById('support-reply-input');
        const content = input.value.trim();
        if (!content || !selectedId) return;
        const button = event.currentTarget.querySelector('button');
        button.disabled = true;
        try {
            const result = await call(`/api/support-chat/${selectedId}/messages`, { method: 'POST', body: JSON.stringify({ content }) });
            input.value = '';
            afterMessageId = Math.max(afterMessageId, result.message.id);
            appendMessage(result.message);
        } catch (error) {
            showError(error.message);
        } finally {
            button.disabled = false;
            input.focus();
        }
    });

    refreshInbox();
    restorePresence();
    inboxTimer = setInterval(refreshInbox, 3000);
})();
