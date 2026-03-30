// === API Helper Module ===
const API_BASE = '/api';

const api = {
    getToken() {
        return localStorage.getItem('dnd_token');
    },

    setToken(token) {
        localStorage.setItem('dnd_token', token);
    },

    clearToken() {
        localStorage.removeItem('dnd_token');
    },

    isLoggedIn() {
        return !!this.getToken();
    },

    authHeaders() {
        return {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${this.getToken()}`
        };
    },

    async request(method, path, body = null, requireAuth = true) {
        const headers = requireAuth ? this.authHeaders() : { 'Content-Type': 'application/json' };
        const options = { method, headers };
        if (body) options.body = JSON.stringify(body);

        const response = await fetch(`${API_BASE}${path}`, options);

        if (response.status === 401) {
            this.clearToken();
            window.location.href = 'login.html';
            return null;
        }

        const data = await response.json().catch(() => null);

        if (!response.ok) {
            const msg = data?.message || `Erro ${response.status}`;
            throw new Error(msg);
        }

        return data;
    },

    // Auth
    async login(username, password) {
        const data = await this.request('POST', '/auth/login', { username, password }, false);
        if (data?.token) {
            this.setToken(data.token);
        }
        return data;
    },

    logout() {
        this.clearToken();
        window.location.href = 'login.html';
    },

    // Sheets
    async getSheets() {
        return this.request('GET', '/sheets');
    },

    async getSheet(id) {
        return this.request('GET', `/sheets/${id}`, null, false);
    },

    async createSheet(characterName, editPassword) {
        return this.request('POST', '/sheets', { characterName, editPassword });
    },

    async updateSheet(id, editPassword, sheetData) {
        return this.request('PUT', `/sheets/${id}`, { editPassword, sheetData });
    },

    async deleteSheet(id, editPassword) {
        return this.request('DELETE', `/sheets/${id}`, { editPassword });
    },

    async verifyPassword(id, editPassword) {
        return this.request('POST', `/sheets/${id}/verify-password`, { editPassword });
    },

    requireAuth() {
        if (!this.isLoggedIn()) {
            window.location.href = 'login.html';
            return false;
        }
        return true;
    }
};
