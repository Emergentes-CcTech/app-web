/**
 * BaseEndpoint class to handle common API endpoint operations.
 * This class provides methods for CRUD operations.
 * It can be extended by specific endpoint classes.
 *
 * @class
 * @param {Object} baseApi - The base API instance containing the HTTP client.
 * @param {string} endpointPath - The specific path for the API endpoint.
 * @example
 * const usersEndpoint = new BaseEndpoint(apiInstance, '/users');
 * usersEndpoint.getAll().then(users => console.log(users));
 */
export class BaseEndpoint {
    constructor(baseApi, endpointPath) {
        this.http = baseApi.http;
        this.endpointPath = endpointPath;
    }

    getAll() {
        return this.http.get(this.endpointPath);
    }

    getById(id) {
        return this.http.get(`${this.endpointPath}/${id}`);
    }

    create(resource) {
        return this.http.post(this.endpointPath, resource);
    }

    update(id, resource) {
        return this.http.put(`${this.endpointPath}/${id}`, resource);
    }

    delete(id) {
        return this.http.delete(`${this.endpointPath}/${id}`);
    }
}