// API Configuration
const API_BASE_URL = 'http://localhost:5000/api';
const TOKEN_KEY = 'authToken';
const USER_KEY = 'currentUser';
const ORG_KEY = 'selectedOrganisation';

// API Service
class ApiService {
    static getToken() {
        return localStorage.getItem(TOKEN_KEY);
    }

    static setToken(token) {
        localStorage.setItem(TOKEN_KEY, token);
    }

    static clearToken() {
        localStorage.removeItem(TOKEN_KEY);
    }

    static getHeaders(includeAuth = true) {
        const headers = {
            'Content-Type': 'application/json',
        };

        if (includeAuth) {
            const token = this.getToken();
            if (token) {
                headers['Authorization'] = `Bearer ${token}`;
            }
        }

        return headers;
    }

    static async request(url, method = 'GET', body = null) {
        const options = {
            method,
            headers: this.getHeaders(),
        };

        if (body) {
            options.body = JSON.stringify(body);
        }

        try {
            const response = await fetch(url, options);

            if (response.status === 401) {
                this.clearToken();
                window.location.href = '/pages/login.html';
            }

            const data = await response.json().catch(() => null);
            return { ok: response.ok, status: response.status, data };
        } catch (error) {
            console.error('API Error:', error);
            return { ok: false, status: 0, data: null, error };
        }
    }

    // Auth Endpoints
    static async register(email, password, firstName, lastName) {
        const response = await this.request(`${API_BASE_URL}/auth/register`, 'POST', {
            email,
            password,
            firstName,
            lastName,
        });
        if (response.ok && response.data.accessToken) {
            this.setToken(response.data.accessToken);
            localStorage.setItem(USER_KEY, JSON.stringify({
                id: response.data.userId,
                email: email,
            }));
        }
        return response;
    }

    static async login(email, password) {
        const response = await this.request(`${API_BASE_URL}/auth/login`, 'POST', {
            email,
            password,
        });
        if (response.ok && response.data.accessToken) {
            this.setToken(response.data.accessToken);
            localStorage.setItem(USER_KEY, JSON.stringify({
                id: response.data.userId,
                email: email,
            }));
        }
        return response;
    }

    static logout() {
        this.clearToken();
        localStorage.removeItem(USER_KEY);
        localStorage.removeItem(ORG_KEY);
    }

    // Organization Endpoints
    static async createOrganisation(name, description, registrationNumber, taxNumber) {
        return this.request(`${API_BASE_URL}/organisations`, 'POST', {
            name,
            description,
            registrationNumber,
            taxNumber,
        });
    }

    static async getOrganisation(id) {
        return this.request(`${API_BASE_URL}/organisations/${id}`);
    }

    static async listOrganisations() {
        return this.request(`${API_BASE_URL}/organisations`);
    }

    static async updateOrganisation(id, data) {
        return this.request(`${API_BASE_URL}/organisations/${id}`, 'PUT', data);
    }

    static async deleteOrganisation(id) {
        return this.request(`${API_BASE_URL}/organisations/${id}`, 'DELETE');
    }

    // Organisation Members
    static async getMembers(orgId) {
        return this.request(`${API_BASE_URL}/organisations/${orgId}/members`);
    }

    static async updateMemberRole(orgId, memberId, role) {
        return this.request(`${API_BASE_URL}/organisations/${orgId}/members/${memberId}`, 'PUT', { role });
    }

    static async removeMember(orgId, memberId) {
        return this.request(`${API_BASE_URL}/organisations/${orgId}/members/${memberId}`, 'DELETE');
    }

    // Organisation Invitations
    static async getInvitations(orgId) {
        return this.request(`${API_BASE_URL}/organisations/${orgId}/invitations`);
    }

    static async createInvitation(orgId, email, role) {
        return this.request(`${API_BASE_URL}/organisations/${orgId}/invitations`, 'POST', { invitedEmail: email, role });
    }

    static async updateOrganisationVatSettings(id, defaultVatAccountId, vatReducedRate, vatFullRate) {
        return this.request(`${API_BASE_URL}/organisations/${id}`, 'PUT', { defaultVatAccountId, vatReducedRate, vatFullRate });
    }

    static async cancelInvitation(orgId, invitationId) {
        return this.request(`${API_BASE_URL}/organisations/${orgId}/invitations/${invitationId}`, 'DELETE');
    }

    static async acceptInvitation(token) {
        return this.request(`${API_BASE_URL}/organisations/invitations/accept`, 'POST', { token });
    }

    // GL Account Endpoints
    static async createGLAccount(organisationId, account) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/glaccounts`,
            'POST',
            account
        );
    }

    static async getGLAccounts(organisationId) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/glaccounts`);
    }

    static async getGLAccount(accountId) {
        return this.request(`${API_BASE_URL}/organisations/${this.getSelectedOrg()}/glaccounts/${accountId}`);
    }

    static async updateGLAccount(organisationId, accountId, data) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/glaccounts/${accountId}`,
            'PUT',
            data
        );
    }

    static async deleteGLAccount(organisationId, accountId) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/glaccounts/${accountId}`,
            'DELETE'
        );
    }

    // Daybook Endpoints
    static async createDaybookEntry(organisationId, entry) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/daybook`,
            'POST',
            entry
        );
    }

    static async createSalesDaybookEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/sales`, 'POST', entry);
    }

    static async createSalesReturnDaybookEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/sales-returns`, 'POST', entry);
    }

    static async createPurchaseDaybookEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/purchases`, 'POST', entry);
    }

    static async createPurchaseReturnDaybookEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/purchase-returns`, 'POST', entry);
    }

    static async createReceiptDaybookEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/receipts`, 'POST', entry);
    }

    static async createPaymentDaybookEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/payments`, 'POST', entry);
    }

    static async getDaybookEntries(organisationId, fromDate = null, toDate = null) {
        let url = `${API_BASE_URL}/organisations/${organisationId}/daybook`;
        const params = new URLSearchParams();
        if (fromDate) params.append('fromDate', fromDate);
        if (toDate) params.append('toDate', toDate);
        if (params.toString()) url += '?' + params.toString();
        return this.request(url);
    }

    static async getDaybookEntry(organisationId, entryId) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/${entryId}`);
    }

    static async postDaybookEntry(organisationId, entryId) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/daybook/${entryId}/post`,
            'POST'
        );
    }

    static async deleteDaybookEntry(organisationId, entryId) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/daybook/${entryId}`,
            'DELETE'
        );
    }

    // Simplified daybook endpoints (no GL account selection required)
    static async createSimpleSalesEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/simple-sales`, 'POST', entry);
    }
    static async createSimpleSalesReturnEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/simple-sales-returns`, 'POST', entry);
    }
    static async createSimplePurchaseEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/simple-purchases`, 'POST', entry);
    }
    static async createSimplePurchaseReturnEntry(organisationId, entry) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/daybook/simple-purchase-returns`, 'POST', entry);
    }

    // Products & Services
    static async getProducts(organisationId) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/products`);
    }
    static async createProduct(organisationId, product) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/products`, 'POST', product);
    }
    static async updateProduct(organisationId, productId, product) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/products/${productId}`, 'PUT', product);
    }
    static async deleteProduct(organisationId, productId) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/products/${productId}`, 'DELETE');
    }

    // Report Endpoints
    static async getTrialBalance(organisationId, asOfDate = null) {
        let url = `${API_BASE_URL}/organisations/${organisationId}/reports/trial-balance`;
        if (asOfDate) url += `?asOfDate=${asOfDate}`;
        return this.request(url);
    }

    static async getTAccounts(organisationId, asOfDate = null) {
        let url = `${API_BASE_URL}/organisations/${organisationId}/reports/taccounts`;
        if (asOfDate) url += `?asOfDate=${asOfDate}`;
        return this.request(url);
    }

    static async getTAccount(organisationId, accountId, asOfDate = null) {
        let url = `${API_BASE_URL}/organisations/${organisationId}/reports/taccounts/${accountId}`;
        if (asOfDate) url += `?asOfDate=${asOfDate}`;
        return this.request(url);
    }

    static async getGeneralLedger(organisationId, fromDate, toDate) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/reports/general-ledger?fromDate=${fromDate}&toDate=${toDate}`
        );
    }

    static async getProfitAndLoss(organisationId, fromDate, toDate) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/reports/profit-and-loss?fromDate=${fromDate}&toDate=${toDate}`
        );
    }

    static async getBalanceSheet(organisationId, asOfDate = null) {
        let url = `${API_BASE_URL}/organisations/${organisationId}/reports/balance-sheet`;
        if (asOfDate) url += `?asOfDate=${asOfDate}`;
        return this.request(url);
    }

    // Customer Endpoints
    static async createCustomer(organisationId, customer) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/customers`,
            'POST',
            customer
        );
    }

    static async getCustomers(organisationId) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/customers`);
    }

    static async getCustomer(organisationId, customerId) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/customers/${customerId}`);
    }

    static async updateCustomer(organisationId, customerId, data) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/customers/${customerId}`,
            'PUT',
            data
        );
    }

    static async deleteCustomer(organisationId, customerId) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/customers/${customerId}`,
            'DELETE'
        );
    }

    // Supplier Endpoints
    static async createSupplier(organisationId, supplier) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/suppliers`,
            'POST',
            supplier
        );
    }

    static async getSuppliers(organisationId) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/suppliers`);
    }

    static async getSupplier(organisationId, supplierId) {
        return this.request(`${API_BASE_URL}/organisations/${organisationId}/suppliers/${supplierId}`);
    }

    static async updateSupplier(organisationId, supplierId, data) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/suppliers/${supplierId}`,
            'PUT',
            data
        );
    }

    static async deleteSupplier(organisationId, supplierId) {
        return this.request(
            `${API_BASE_URL}/organisations/${organisationId}/suppliers/${supplierId}`,
            'DELETE'
        );
    }

    // Helper Methods
    static setSelectedOrg(orgId) {
        localStorage.setItem(ORG_KEY, orgId);
    }

    static getSelectedOrg() {
        return localStorage.getItem(ORG_KEY);
    }

    static async populateOrganisationSelector() {
        const response = await this.listOrganisations();
        if (response.ok) {
            const orgs = response.data;
            const select = document.getElementById('organisationSelect');
            select.innerHTML = '<option value="">Select Organisation</option>';
            orgs.forEach(o => {
                const opt = document.createElement('option');
                opt.value = o.id;
                opt.textContent = o.name;
                select.appendChild(opt);
            });
            const saved = this.getSelectedOrg();
            if (saved) select.value = saved;
        }
        return response;
    }

    static getCurrentUser() {
        const user = localStorage.getItem(USER_KEY);
        return user ? JSON.parse(user) : null;
    }

    static isLoggedIn() {
        return !!this.getToken();
    }
}

// UI Utility Functions
class UIUtils {
    static getErrorMessage(data) {
        if (!data) return 'An unexpected error occurred. Please check your connection.';
        // Custom exception middleware format: { error: { code, message } }
        if (data.error?.message) return data.error.message;
        // ASP.NET ProblemDetails format: { title, errors: { field: [msg] } }
        if (data.errors) {
            const msgs = Object.values(data.errors).flat();
            return msgs.length > 0 ? msgs.join(' | ') : (data.title || 'Validation error');
        }
        if (data.title) return data.title;
        return 'An unexpected error occurred.';
    }

    static showAlert(message, type = 'info') {
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type}`;
        alertDiv.textContent = message;
        alertDiv.style.position = 'fixed';
        alertDiv.style.top = '80px';
        alertDiv.style.right = '20px';
        alertDiv.style.zIndex = '1000';
        alertDiv.style.maxWidth = '400px';

        document.body.appendChild(alertDiv);

        setTimeout(() => {
            alertDiv.remove();
        }, 5000);
    }

    static showLoading() {
        const loader = document.createElement('div');
        loader.id = 'loading-spinner';
        loader.style.cssText = `
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            z-index: 2000;
            font-size: 18px;
            color: var(--primary-color);
        `;
        loader.innerHTML = '<p>Loading...</p>';
        document.body.appendChild(loader);
    }

    static hideLoading() {
        const loader = document.getElementById('loading-spinner');
        if (loader) loader.remove();
    }

    static formatCurrency(value) {
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: 'USD',
        }).format(value);
    }

    static formatDate(date) {
        return new Date(date).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
        });
    }
}

export { ApiService, UIUtils, AuthGuard };

// ── Auth Guard ────────────────────────────────────────────────────────────────
// Call AuthGuard.init() at the top of every protected page script.
// Handles:
//   • Immediate redirect if no token in localStorage
//   • Background server-side session verification on load
//   • Periodic re-verification every 5 minutes (catches expired JWTs)
//   • Re-verification when the tab regains focus (handles sleep / long idle)
//   • Cross-tab logout detection via the storage event

class AuthGuard {
    static _intervalId = null;
    static _POLL_MS = 5 * 60 * 1000; // 5 minutes
    static _bound = false;

    static init() {
        // 1. Immediate client-side gate
        if (!ApiService.isLoggedIn()) {
            this._redirect();
            return;
        }

        // 2. Background server-side verify (non-blocking — bad token caught here)
        this._verify();

        // 3. Periodic polling
        clearInterval(this._intervalId);
        this._intervalId = setInterval(() => this._verify(), this._POLL_MS);

        // 4. Re-verify when the user returns to the tab after idling / sleep
        if (!this._bound) {
            this._bound = true;

            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'visible') this._verify();
            });

            // 5. Detect logout performed in another browser tab
            window.addEventListener('storage', (e) => {
                if (e.key === TOKEN_KEY && !e.newValue) this._redirect();
            });
        }
    }

    static async _verify() {
        // Client-side token presence check first (fast path)
        if (!ApiService.isLoggedIn()) {
            this._redirect();
            return;
        }
        // Any authenticated request triggers the global 401 handler in
        // ApiService.request() which clears the token and redirects.
        // We use listOrganisations as a lightweight ping.
        // Network errors (status 0) are ignored — don't boot users for flaky wifi.
        const response = await ApiService.listOrganisations();
        if (!response.ok && response.status !== 0) {
            // Non-network failure (e.g. 401, 403) → session gone
            this._redirect();
        }
    }

    static _redirect() {
        clearInterval(this._intervalId);
        this._intervalId = null;
        window.location.href = 'login.html';
    }
}
