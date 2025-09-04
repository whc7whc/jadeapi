'use strict';

/**
 * 客服聊天系統
 * 版本: 1.0
 * 作者: 您的小組
 */

// 客服聊天類別
class CustomerServiceChat {
    constructor() {
        this.messages = [];
        this.isInitialized = false;
        this.init();
    }

    /**
     * 初始化客服聊天系統
     */
    init() {
        if (this.isInitialized) return;

        this.bindEvents();
        this.loadWelcomeMessage();
        this.isInitialized = true;
    }

    /**
     * 綁定事件監聽器
     */
    bindEvents() {
        // 發送訊息事件
        const sendButton = document.getElementById('customerSendButton');
        const messageInput = document.getElementById('customerMessageInput');
        const clearButton = document.getElementById('clearCustomerChat');

        if (sendButton) {
            sendButton.addEventListener('click', () => {
                this.sendMessage();
            });
        }

        if (messageInput) {
            messageInput.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') {
                    this.sendMessage();
                }
            });
        }

        if (clearButton) {
            clearButton.addEventListener('click', () => {
                this.clearChat();
            });
        }

        // Modal 開啟時聚焦輸入框
        $(document).ready(function () {
            $('#customerServiceModal').on('shown.bs.modal', function () {
                $('#customerMessageInput').focus();
            });
        });
    }

    /**
     * 載入歡迎訊息
     */
    loadWelcomeMessage() {
        setTimeout(() => {
            this.addMessageToChat('agent', '您好！我是客服專員小玉，很高興為您服務！請問有什麼可以幫助您的嗎？');
        }, 1000);
    }

    /**
     * 發送用戶訊息
     */
    sendMessage() {
        const input = document.getElementById('customerMessageInput');
        if (!input) return;

        const message = input.value.trim();
        if (!message) return;

        // 添加用戶訊息
        this.addMessageToChat('user', message);
        input.value = '';

        // 儲存訊息到陣列
        this.messages.push({
            role: 'user',
            content: message,
            timestamp: new Date()
        });

        // 模擬客服回覆（這裡可以整合真實的客服系統或OpenAI API）
        this.simulateAgentResponse(message);
    }

    /**
     * 客服回覆 (整合 OpenAI API)
     * @param {string} userMessage - 用戶訊息
     */
    async simulateAgentResponse(userMessage) {
        // 顯示正在輸入指示器
        this.showTypingIndicator();

        try {
            // 呼叫後端 OpenAI API
            const response = await this.callOpenAIAPI(userMessage);

            this.hideTypingIndicator();
            this.addMessageToChat('agent', response);

            // 儲存客服回覆
            this.messages.push({
                role: 'assistant',
                content: response,
                timestamp: new Date()
            });

        } catch (error) {
            console.error('客服回覆錯誤:', error);
            this.hideTypingIndicator();

            // 發生錯誤時使用備用回覆
            const fallbackResponse = this.generateAutoResponse(userMessage);
            this.addMessageToChat('agent', fallbackResponse);

            this.messages.push({
                role: 'assistant',
                content: fallbackResponse,
                timestamp: new Date()
            });
        }
    }

    /**
     * 呼叫 OpenAI API
     * @param {string} message - 用戶訊息
     * @returns {Promise<string>} API 回覆
     */
    async callOpenAIAPI(message) {
        // 獲取防偽令牌
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        // 準備對話歷史 (最近 10 條訊息)
        const recentMessages = this.messages.slice(-10).map(msg => ({
            Role: msg.role === 'agent' ? 'assistant' : msg.role,
            Content: msg.content
        }));

        // 添加當前用戶訊息
        recentMessages.push({
            Role: 'user',
            Content: message
        });

        const requestData = {
            Messages: recentMessages
        };

        // Note: controller route is api/OpenAI/CustomerService
        const endpoint = '/api/OpenAI/CustomerService';

        const response = await fetch(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token || ''
            },
            body: JSON.stringify(requestData)
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(`HTTP error! status: ${response.status} - ${text}`);
        }

        const data = await response.json();

        if (data.success) {
            return data.response || data.reply || '';
        } else {
            throw new Error(data.error || 'OpenAI 回應錯誤');
        }
    }

    /**
     * 生成自動回覆內容
     * @param {string} message - 用戶訊息
     * @returns {string} 回覆內容
     */
    generateAutoResponse(message) {
        const lowerMessage = message.toLowerCase();

        // 根據關鍵字回覆
        if (lowerMessage.includes('價格') || lowerMessage.includes('費用') || lowerMessage.includes('收費')) {
            return '關於價格問題，我們有多種方案可以選擇。請您稍等，我為您查詢最新的價格資訊...';
        }
        else if (lowerMessage.includes('問題') || lowerMessage.includes('錯誤') || lowerMessage.includes('故障')) {
            return '我了解您遇到了問題，請您詳細描述一下具體的情況，我會盡力幫您解決。';
        }
        else if (lowerMessage.includes('謝謝') || lowerMessage.includes('感謝')) {
            return '不客氣！這是我應該做的。還有其他需要幫助的地方嗎？';
        }
        else if (lowerMessage.includes('再見') || lowerMessage.includes('結束')) {
            return '感謝您使用我們的服務！如果之後還有任何問題，隨時歡迎聯繫我們。祝您有美好的一天！';
        }
        else if (lowerMessage.includes('訂單') || lowerMessage.includes('購買')) {
            return '關於訂單問題，我可以幫您查詢訂單狀態。請提供您的訂單編號，我會立即為您處理。';
        }
        else if (lowerMessage.includes('退貨') || lowerMessage.includes('退款')) {
            return '關於退貨退款事宜，我們有完善的退換貨政策。請告訴我具體情況，我會協助您辦理。';
        }
        else if (lowerMessage.includes('配送') || lowerMessage.includes('物流')) {
            return '關於配送問題，我們與多家物流公司合作確保快速送達。一般來說，標準配送需要3-5個工作天。';
        }
        else {
            const responses = [
                '我已經收到您的訊息，正在為您處理。如果需要更詳細的協助，我也可以為您轉接到專業的技術支援團隊。',
                '感謝您的提問！我正在為您查詢相關資訊，請稍候片刻。',
                '我理解您的需求，讓我為您提供最適合的解決方案。',
                '這是一個很好的問題！我會盡我所能為您提供協助。'
            ];
            return responses[Math.floor(Math.random() * responses.length)];
        }
    }

    /**
     * 添加訊息到聊天容器
     * @param {string} role - 角色 ('user' 或 'agent')
     * @param {string} content - 訊息內容
     */
    addMessageToChat(role, content) {
        const chatContainer = document.getElementById('customerChatContainer');
        if (!chatContainer) return;

        const messageDiv = document.createElement('div');
        messageDiv.className = `mb-3 ${role === 'user' ? 'text-end' : 'text-start'}`;

        let iconClass, bgClass, senderName, messageClass;
        if (role === 'user') {
            iconClass = 'fas fa-user';
            bgClass = 'user-message';
            senderName = '您';
            messageClass = 'user-message';
        } else {
            iconClass = 'fas fa-headset';
            bgClass = 'agent-message';
            senderName = '客服小玉';
            messageClass = 'agent-message';
        }

        messageDiv.innerHTML = `
            <div class="d-inline-block p-3 shadow-sm customer-message ${messageClass}">
                <div class="mb-1">
                    <i class="${iconClass}"></i>
                    <strong>${senderName}</strong>
                    <small class="text-muted ml-2" style="opacity: 0.7;">${new Date().toLocaleTimeString()}</small>
                </div>
                <div>${this.escapeHtml(content)}</div>
            </div>
        `;

        chatContainer.appendChild(messageDiv);
        chatContainer.scrollTop = chatContainer.scrollHeight;

        // 移除歡迎訊息
        const welcomeText = chatContainer.querySelector('.text-muted.text-center');
        if (welcomeText && chatContainer.children.length > 1) {
            welcomeText.remove();
        }
    }

    /**
     * 顯示正在輸入指示器
     */
    showTypingIndicator() {
        const chatContainer = document.getElementById('customerChatContainer');
        if (!chatContainer) return;

        // 如果已經有正在輸入指示器，就不要重複添加
        if (document.getElementById('typingIndicator')) return;

        const typingDiv = document.createElement('div');
        typingDiv.id = 'typingIndicator';
        typingDiv.className = 'mb-3 text-start';
        typingDiv.innerHTML = `
            <div class="d-inline-block p-3 shadow-sm customer-message agent-message">
                <div class="mb-1">
                    <i class="fas fa-headset"></i>
                    <strong>客服小玉</strong>
                </div>
                <div>
                    <i class="fas fa-circle" style="animation: blink 1.4s infinite;"></i>
                    <i class="fas fa-circle" style="animation: blink 1.4s infinite 0.2s;"></i>
                    <i class="fas fa-circle" style="animation: blink 1.4s infinite 0.4s;"></i>
                    正在輸入...
                </div>
            </div>
        `;

        chatContainer.appendChild(typingDiv);
        chatContainer.scrollTop = chatContainer.scrollHeight;
    }

    /**
     * 隱藏正在輸入指示器
     */
    hideTypingIndicator() {
        const typingIndicator = document.getElementById('typingIndicator');
        if (typingIndicator) {
            typingIndicator.remove();
        }
    }

    /**
     * 清除聊天記錄
     */
    clearChat() {
        const chatContainer = document.getElementById('customerChatContainer');
        if (!chatContainer) return;

        chatContainer.innerHTML = `
            <div class="text-muted text-center">
                <i class="fas fa-headset fa-2x mb-2"></i>
                <p>歡迎使用線上客服！我們將在最短時間內回覆您。</p>
            </div>
        `;

        this.messages = [];
        this.loadWelcomeMessage();
    }

    /**
     * HTML 轉義函數，防止 XSS 攻擊
     * @param {string} text - 要轉義的文字
     * @returns {string} 轉義後的文字
     */
    escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, function (m) { return map[m]; });
    }

    /**
     * 獲取聊天記錄
     * @returns {Array} 聊天記錄陣列
     */
    getChatHistory() {
        return this.messages;
    }

    /**
     * 設置自定義回覆函數（用於整合真實 API）
     * @param {Function} responseFunction - 自定義回覆函數
     */
    setCustomResponseFunction(responseFunction) {
        this.customResponseFunction = responseFunction;
    }
}

// 全域客服聊天實例
let customerServiceChat;

/**
 * 開啟線上客服對話框
 */
function openLiveChat() {
    // 初始化客服聊天（如果還沒初始化）
    if (!customerServiceChat) {
        customerServiceChat = new CustomerServiceChat();
    }

    // 顯示客服對話框
    if (typeof $ !== 'undefined') {
        $('#customerServiceModal').modal('show');
    } else {
        console.error('jQuery is required for the customer service modal');
    }
}

/**
 * 關閉線上客服對話框
 */
function closeLiveChat() {
    if (typeof $ !== 'undefined') {
        $('#customerServiceModal').modal('hide');
    }
}

/**
 * 獲取客服聊天實例
 * @returns {CustomerServiceChat|null} 客服聊天實例
 */
function getCustomerServiceChat() {
    return customerServiceChat;
}

// 當 DOM 準備好時自動初始化
document.addEventListener('DOMContentLoaded', function () {
    // 這裡可以做一些初始化工作
    console.log('Customer Service System Loaded');
});

// 導出給需要的地方使用
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        CustomerServiceChat,
        openLiveChat,
        closeLiveChat,
        getCustomerServiceChat
    };
}