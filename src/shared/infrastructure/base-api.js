import axios from 'axios';
import { TOKEN_KEY, CURRENT_USER_KEY } from './storage-keys.js';
import { ROUTES } from './paths.js';
import { demoAdapter } from './demo-api.js';

const platformApi = import.meta.env.VITE_API_URL;

export class BaseApi {
    #http;
    constructor() {
        this.#http = axios.create({
            baseURL: platformApi,
            // Demo publicada sin backend: las peticiones se responden en el navegador con server/db.json
            ...(import.meta.env.VITE_DEMO === 'true' ? { adapter: demoAdapter } : {})
        });

        // Add a request interceptor to include the JWT token
        this.#http.interceptors.request.use(
            (config) => {
                const token = localStorage.getItem(TOKEN_KEY);
                if (token) {
                    config.headers.Authorization = `Bearer ${token}`;
                }
                return config;
            },
            (error) => {
                return Promise.reject(error);
            }
        );

        // Add a response interceptor to handle 401 Unauthorized
        this.#http.interceptors.response.use(
            (response) => response,
            (error) => {
                if (error.response?.status === 401) {
                    // Si YA estamos en una página pública (/login, /register-*),
                    // no redirigir — dejar que el componente muestre el error
                    const path = window.location.pathname;
                    const isPublicPage = path === ROUTES.LOGIN
                        || path === ROUTES.IAM_LOGIN
                        || path.startsWith(`${ROUTES.IAM_BASE}/register`);
                    if (!isPublicPage) {
                        localStorage.removeItem(TOKEN_KEY);
                        localStorage.removeItem(CURRENT_USER_KEY);
                        window.location.href = ROUTES.IAM_LOGIN;
                    }
                }
                return Promise.reject(error);
            }
        );
    }

    get http() {
        return this.#http;
    }
}
