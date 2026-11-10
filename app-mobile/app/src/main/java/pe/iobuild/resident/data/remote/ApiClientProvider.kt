package pe.iobuild.resident.data.remote

import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import pe.iobuild.resident.BuildConfig
import pe.iobuild.resident.data.local.SessionManager
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

/**
 * Proveedor de cliente Retrofit con inyección automática del JWT Bearer Token.
 * Registra `HttpLoggingInterceptor` únicamente en compilaciones `debug` con `redactHeader("Authorization")`.
 */
class ApiClientProvider(private val sessionManager: SessionManager) {

    @Volatile
    private var cachedBaseUrl: String? = null

    @Volatile
    private var cachedApi: IoBuildMobileBffApi? = null

    fun getApi(): IoBuildMobileBffApi {
        val currentBaseUrl = sessionManager.apiBaseUrl
        val existing = cachedApi
        if (existing != null && cachedBaseUrl == currentBaseUrl) {
            return existing
        }

        synchronized(this) {
            val authInterceptor = Interceptor { chain ->
                val original = chain.request()
                val builder = original.newBuilder()
                    .header("Accept", "application/json")
                val token = sessionManager.token
                if (!token.isNullOrBlank()) {
                    builder.header("Authorization", "Bearer $token")
                }
                chain.proceed(builder.build())
            }

            val clientBuilder = OkHttpClient.Builder()
                .addInterceptor(authInterceptor)
                .connectTimeout(15, TimeUnit.SECONDS)
                .readTimeout(20, TimeUnit.SECONDS)
                .writeTimeout(15, TimeUnit.SECONDS)

            if (BuildConfig.DEBUG) {
                val logging = HttpLoggingInterceptor().apply {
                    level = HttpLoggingInterceptor.Level.BODY
                    redactHeader("Authorization")
                }
                clientBuilder.addInterceptor(logging)
            }

            val client = clientBuilder.build()

            val retrofit = Retrofit.Builder()
                .baseUrl(currentBaseUrl)
                .client(client)
                .addConverterFactory(GsonConverterFactory.create())
                .build()

            val created = retrofit.create(IoBuildMobileBffApi::class.java)
            cachedBaseUrl = currentBaseUrl
            cachedApi = created
            return created
        }
    }
}
