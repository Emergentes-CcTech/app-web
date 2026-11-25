import Foundation

enum APIError: Error {
    case status(Int)
    case transport(Error)
    case decoding(Error)

    var statusCode: Int? {
        if case .status(let code) = self { return code }
        return nil
    }
}

struct EmptyBody: Codable {}

/// Cliente HTTP del backend IoBuild: agrega el JWT, decodifica JSON y nunca registra el token.
final class APIClient {
    private let session: SessionStore
    private let urlSession: URLSession
    private let decoder = JSONDecoder()
    private let encoder = JSONEncoder()

    init(session: SessionStore) {
        self.session = session
        let configuration = URLSessionConfiguration.default
        configuration.timeoutIntervalForRequest = 20
        configuration.requestCachePolicy = .reloadIgnoringLocalCacheData
        urlSession = URLSession(configuration: configuration)
    }

    func get<T: Decodable>(_ path: String, query: [URLQueryItem] = []) async throws -> T {
        let data = try await raw(path, method: "GET", query: query, body: nil)
        return try decode(data)
    }

    func post<T: Decodable, B: Encodable>(_ path: String, body: B) async throws -> T {
        let data = try await raw(path, method: "POST", body: try encoder.encode(body))
        return try decode(data)
    }

    func put<T: Decodable, B: Encodable>(_ path: String, body: B) async throws -> T {
        let data = try await raw(path, method: "PUT", body: try encoder.encode(body))
        return try decode(data)
    }

    /// Envía la petición sin decodificar la respuesta (cuando solo importa el código HTTP).
    func send<B: Encodable>(_ path: String, method: String, body: B?) async throws {
        _ = try await raw(path, method: method, body: try body.map { try encoder.encode($0) })
    }

    @discardableResult
    func raw(_ path: String, method: String, query: [URLQueryItem] = [], body: Data?) async throws -> Data {
        guard var components = URLComponents(string: session.baseURL + path) else {
            throw APIError.transport(URLError(.badURL))
        }
        if !query.isEmpty { components.queryItems = query }
        guard let url = components.url else { throw APIError.transport(URLError(.badURL)) }

        var request = URLRequest(url: url)
        request.httpMethod = method
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if let token = session.token, !token.isEmpty {
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }
        if let body {
            request.httpBody = body
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }

        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await urlSession.data(for: request)
        } catch {
            throw APIError.transport(error)
        }
        let code = (response as? HTTPURLResponse)?.statusCode ?? 0
        guard (200..<300).contains(code) else { throw APIError.status(code) }
        return data
    }

    private func decode<T: Decodable>(_ data: Data) throws -> T {
        do {
            return try decoder.decode(T.self, from: data.isEmpty ? Data("{}".utf8) : data)
        } catch {
            throw APIError.decoding(error)
        }
    }
}
