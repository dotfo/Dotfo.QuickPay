using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace QuickPay
{
    public static class QuickPayHelpers
    {

        public static bool IsValid(HttpRequest request, QuickPaySettings settings)
        {

            string checkSum = request.Headers["QuickPay-Checksum-Sha256"];

            request.Body.Seek(0, SeekOrigin.Begin);
            var content = new StreamReader(request.Body).ReadToEnd();

            string compute = Sign(content, settings.PrivateKey);

            if (checkSum.Equals(compute))
            {
                return true;
            }
            return false;
        }

        private static string Sign(string content, string apiKey)
        {
            var e = Encoding.UTF8;

            var hmac = new HMACSHA256(e.GetBytes(apiKey));
            byte[] b = hmac.ComputeHash(e.GetBytes(content));

            var s = new StringBuilder();
            foreach (byte t in b)
            {
                s.Append(t.ToString("x2"));
            }

            return s.ToString();
        }
    }

    public class QuickPayClient
    {
        private readonly ILogger _logger;

        public QuickPayClient(QuickPaySettings quickPaySettings)
        {
            new QuickPayClient(quickPaySettings, null);
        }

        public QuickPayClient(QuickPaySettings quickPaySettings, ILogger<QuickPayClient> logger)
        {
            Settings = quickPaySettings;
            _logger = logger;
        }

        private void LogInformation(string message)
        {
            if (_logger != null)
            {
                _logger.LogInformation(message);
            }
        } 

        public async Task<QuickPayCallback> CreatePayment(QuickPayCreatePayment model)
        {
            return await Post<QuickPayCreatePayment, QuickPayCallback>(model, "payments");
        }

        public async Task<QuickPayCallback> GetPayment(int id)
        {
            return await Get<QuickPayCreatePayment, QuickPayCallback>($"payments/{id}");
        }

        public async Task<QuickPayLink> CreateLink(QuickPayCreateLink model, int id)
        {
            return await Put<QuickPayCreateLink, QuickPayLink>(model, $"payments/{id}/link");
        }

        public async Task<QuickPayCallback> RefundPayment(int id, QuickPayRefund model)
        {
            return await Post<QuickPayRefund, QuickPayCallback>(model, $"payments/{id}/refund");
        }

        public async Task<QuickPayCallback> CapturePayment(int id, QuickPayCapture model)
        {
            return await Post<QuickPayCapture, QuickPayCallback>(model, $"payments/{id}/capture");
        }

        public async Task<QuickPayCallback> CreateSubscription(QuickPayCreatePayment model)
        {
            return await Post<QuickPayCreatePayment, QuickPayCallback>(model, "subscriptions");
        }

        public async Task<QuickPayCallback> GetSubscription(QuickPayGetSubscription model)
        {
            return await Get<QuickPayGetSubscription, QuickPayCallback>($"subscriptions/{model.Id}");
        }

        public async Task<QuickPayLink> CreateSubscriptionLink(QuickPayCreateLink model, int id)
        {
            return await Put<QuickPayCreateLink, QuickPayLink>(model, $"subscriptions/{id}/link");
        }

        public async Task<QuickPayCallback> CreateRecurring(QuickPayCreateRecurring model, int id)
        {
            return await Post<QuickPayCreateRecurring, QuickPayCallback>(model, $"subscriptions/{id}/recurring");
        }

        public async Task<QuickPayCallback> CancelSubscription(QuickPayCancelSubscription model)
        {
            return await Post<QuickPayCancelSubscription, QuickPayCallback>(model,
                $"subscriptions/{model.Id}/cancel");
        }



        public QuickPaySettings Settings { get; }

        private async Task<TK> Get<T, TK>(string url)
        {
            return await Get<T, TK>(new QuickPayEmpty(), url);
        }

        private async Task<TK> Get<T, TK>(QuickPayPayload model, string url)
        {
            var authValue = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{Settings.ApiKey}")));
            HttpClient client = new HttpClient()
            {
                DefaultRequestHeaders = { Authorization = authValue }
            };
            client.DefaultRequestHeaders.Add("Accept-Version", model.Headers.ContainsKey("Accept-Version") ? model.Headers["Accept-Version"] : Settings.Version);
            client.DefaultRequestHeaders.Add("QuickPay-Callback-Url", model.Headers.ContainsKey("QuickPay-Callback-Url") ? model.Headers["QuickPay-Callback-Url"] : Settings.CallbackUrl);

            var response = await client.GetAsync(Settings.EndPoint + url);

            if (!response.IsSuccessStatusCode)
            {
                throw new QuickPayException(response.ReasonPhrase, response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            var responseValue = await response.Content.ReadAsStringAsync();

            LogInformation(responseValue);
            return JsonConvert.DeserializeObject<TK>(responseValue);
        }

        private async Task<TK> Post<T, TK>(QuickPayPayload model, string url)
        {
            var authValue = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{Settings.ApiKey}")));
            HttpClient client = new HttpClient()
            {
                DefaultRequestHeaders = { Authorization = authValue }
            };
            client.DefaultRequestHeaders.Add("Accept-Version", model.Headers.ContainsKey("Accept-Version") ? model.Headers["Accept-Version"] : Settings.Version);
            client.DefaultRequestHeaders.Add("QuickPay-Callback-Url", model.Headers.ContainsKey("QuickPay-Callback-Url") ? model.Headers["QuickPay-Callback-Url"] : Settings.CallbackUrl);

            var data = JsonConvert.SerializeObject(model);

            var response = await client.PostAsync(Settings.EndPoint + url,
                new StringContent(data, Encoding.UTF8, "application/json"));

            var responseValue = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new QuickPayException(response.ReasonPhrase, response.StatusCode, await response.Content.ReadAsStringAsync());
            }

            LogInformation(responseValue);
            return JsonConvert.DeserializeObject<TK>(responseValue);
        }

        private async Task<TK> Put<T, TK>(QuickPayPayload model, string url)
        {
            var authValue = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{Settings.ApiKey}")));
            HttpClient client = new HttpClient()
            {
                DefaultRequestHeaders = { Authorization = authValue }
            };
            client.DefaultRequestHeaders.Add("Accept-Version", model.Headers.ContainsKey("Accept-Version") ? model.Headers["Accept-Version"] : Settings.Version);
            client.DefaultRequestHeaders.Add("QuickPay-Callback-Url", model.Headers.ContainsKey("QuickPay-Callback-Url") ? model.Headers["QuickPay-Callback-Url"] : Settings.CallbackUrl);

            var response = await client.PutAsync(Settings.EndPoint + url,
                new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                throw new QuickPayException(response.ReasonPhrase, response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            var responseValue = await response.Content.ReadAsStringAsync();

            LogInformation(responseValue);
            return JsonConvert.DeserializeObject<TK>(responseValue);
        }


    }

    public abstract class QuickPayPayload
    {
        [JsonIgnore]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }

    public class QuickPayEmpty : QuickPayPayload
    {

    }

    public class QuickPayRefund : QuickPayPayload
    {
        [JsonProperty("amount")]
        public int Amount { get; set; }
        [JsonProperty("order_id")]
        public string OrderId { get; set; }
    }

    public class QuickPayCapture : QuickPayPayload
    {
        [JsonProperty("amount")]
        public int Amount { get; set; }              
    }

    public class QuickPayCancelSubscription : QuickPayPayload
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class QuickPayGetSubscription : QuickPayPayload
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class QuickPayCreatePayment : QuickPayPayload
    {
        [JsonProperty("order_id")]
        public string OrderId { get; set; }
        [JsonProperty("currency")]
        public string Currency { get; set; } = "dkk";
        [JsonProperty("description")]
        public string Description { get; set; }
    }

    public class QuickPayCreateRecurring : QuickPayPayload
    {
        [JsonProperty("order_id")]
        public string OrderId { get; set; }
        [JsonProperty("currency")]
        public string Currency { get; set; } = "dkk";
        [JsonProperty("description")]
        public string Description { get; set; }
        [JsonProperty("auto_capture")]
        public bool AutoCapture { get; set; }

        [JsonProperty("amount")]
        public double Amount { get; set; }

    }


    public class QuickPayCreateLink : QuickPayPayload
    {
        [JsonProperty("callback_url")]
        public string CallbackUrl { get; set; }
        [JsonProperty("cancel_url")]
        public string CancelUrl { get; set; }
        [JsonProperty("continue_url")]
        public string ContinueUrl { get; set; }

        [JsonProperty("amount")]
        public double Amount { get; set; }
        [JsonProperty("auto_capture")]
        public bool AutoCapture { get; set; }
        [JsonProperty("framed")]
        public bool Framed { get; set; }
        [JsonProperty("language")]
        public string Language { get; set; } = "en";

    }

    public class QuickPayLink : QuickPayPayload
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class QuickPaySettings
    {
        public string ApiKey { get; set; }
        public string PrivateKey { get; set; }
        public string CallbackUrl { get; set; }
        public string CancelUrl { get; set; }
        public string ContinueUrl { get; set; }
        public string Version { get; set; } = "v10";
        public string EndPoint { get; set; } = "https://api.quickpay.net/";
    }

    public class QuickPayException : Exception
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public string ResponseValue { get; set; }

        public QuickPayException()
        {
        }

        public QuickPayException(string message) : base(message)
        {
        }

        public QuickPayException(string message, HttpStatusCode httpStatusCode) : base(message)
        {
            HttpStatusCode = httpStatusCode;
        }

        public QuickPayException(string message, HttpStatusCode httpStatusCode, string responseValue) : base(message)
        {
            HttpStatusCode = httpStatusCode;
            ResponseValue = responseValue;
        }

        public QuickPayException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public QuickPayException(string message, Exception innerException, HttpStatusCode httpStatusCode) : base(message, innerException)
        {
            HttpStatusCode = httpStatusCode;
        }

        public QuickPayException(string message, Exception innerException, HttpStatusCode httpStatusCode, string responseValue) : base(message, innerException)
        {
            HttpStatusCode = httpStatusCode;
            ResponseValue = responseValue;
        }
    }


    public class QuickPayVariables
    {

    }


    public class QuickPayMetadata
    {

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("origin")]
        public string Origin { get; set; }

        [JsonProperty("brand")]
        public string Brand { get; set; }

        [JsonProperty("bin")]
        public string Bin { get; set; }

        [JsonProperty("last4")]
        public string Last4 { get; set; }

        [JsonProperty("exp_month")]
        public int? ExpMonth { get; set; }

        [JsonProperty("exp_year")]
        public int? ExpYear { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("is_3d_secure")]
        public bool? Is_3DSecure { get; set; }

        [JsonProperty("issued_to")]
        public object IssuedTo { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("number")]
        public object Number { get; set; }

        [JsonProperty("customer_ip")]
        public string CustomerIp { get; set; }

        [JsonProperty("customer_country")]
        public string CustomerCountry { get; set; }

        [JsonProperty("fraud_suspected")]
        public bool FraudSuspected { get; set; }

        [JsonProperty("fraud_remarks")]
        public IList<object> FraudComments { get; set; }

        [JsonProperty("nin_number")]
        public object NinNumber { get; set; }

        [JsonProperty("nin_country_code")]
        public object NinCountryCode { get; set; }

        [JsonProperty("nin_gender")]
        public object NinGender { get; set; }
    }

    public class QuickPayBrandingConfig
    {
    }

    public class QuickPayLinkCallback
    {

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("agreement_id")]
        public int AgreementId { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("continue_url")]
        public object ContinueUrl { get; set; }

        [JsonProperty("cancel_url")]
        public object CancelUrl { get; set; }

        [JsonProperty("callback_url")]
        public string CallbackUrl { get; set; }

        [JsonProperty("payment_methods")]
        public object PaymentMethods { get; set; }

        [JsonProperty("auto_fee")]
        public bool AutoFee { get; set; }

        [JsonProperty("auto_capture")]
        public object AutoCapture { get; set; }

        [JsonProperty("branding_id")]
        public object BrandingId { get; set; }

        [JsonProperty("google_analytics_client_id")]
        public object GoogleAnalyticsClientId { get; set; }

        [JsonProperty("google_analytics_tracking_id")]
        public object GoogleAnalyticsTrackingId { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("acquirer")]
        public object Acquirer { get; set; }

        [JsonProperty("deadline")]
        public object Deadline { get; set; }

        [JsonProperty("framed")]
        public bool Framed { get; set; }

        [JsonProperty("branding_config")]
        public QuickPayBrandingConfig BrandingConfig { get; set; }

        [JsonProperty("invoice_address_selection")]
        public object InvoiceAddressSelection { get; set; }

        [JsonProperty("shipping_address_selection")]
        public object ShippingAddressSelection { get; set; }

        [JsonProperty("customer_email")]
        public object CustomerEmail { get; set; }
    }

    public class QuickPayData
    {
    }

    public class QuickPayOperation
    {

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("amount")]
        public int? Amount { get; set; }

        [JsonProperty("pending")]
        public bool Pending { get; set; }

        [JsonProperty("qp_status_code")]
        public string QpStatusCode { get; set; }

        [JsonProperty("qp_status_msg")]
        public string QpStatusMsg { get; set; }

        [JsonProperty("aq_status_code")]
        public string AqStatusCode { get; set; }

        [JsonProperty("aq_status_msg")]
        public string AqStatusMsg { get; set; }

        [JsonProperty("data")]
        public QuickPayData Data { get; set; }

        [JsonProperty("callback_url")]
        public string CallbackUrl { get; set; }

        [JsonProperty("callback_success")]
        public bool CallbackSuccess { get; set; }

        [JsonProperty("callback_response_code")]
        public string CallbackResponseCode { get; set; }

        [JsonProperty("callback_duration")]
        public int CallbackDuration { get; set; }

        [JsonProperty("acquirer")]
        public string Acquirer { get; set; }

        [JsonProperty("callback_at")]
        public object CallbackAt { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    public partial class QuickPayCallback
    {
        [JsonProperty("deadline_at")]
        public object DeadlineAt { get; set; }

        [JsonProperty("operations")]
        public Operation[] Operations { get; set; }

        [JsonProperty("basket")]
        public object[] Basket { get; set; }

        [JsonProperty("acquirer")]
        public string Acquirer { get; set; }

        [JsonProperty("accepted")]
        public bool? Accepted { get; set; }

        [JsonProperty("balance")]
        public long? Balance { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("branding_id")]
        public object BrandingId { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("invoice_address")]
        public object InvoiceAddress { get; set; }

        [JsonProperty("fee")]
        public object Fee { get; set; }

        [JsonProperty("facilitator")]
        public object Facilitator { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("merchant_id")]
        public long? MerchantId { get; set; }

        [JsonProperty("link")]
        public Link Link { get; set; }

        [JsonProperty("metadata")]
        public Metadata Metadata { get; set; }

        [JsonProperty("shipping_address")]
        public object ShippingAddress { get; set; }

        [JsonProperty("retented_at")]
        public object RetentedAt { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        [JsonProperty("shipping")]
        public object Shipping { get; set; }

        [JsonProperty("test_mode")]
        public bool? TestMode { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("text_on_statement")]
        public object TextOnStatement { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }

        [JsonProperty("variables")]
        public BrandingConfig Variables { get; set; }
    }

    public partial class Operation
    {
        [JsonProperty("callback_success")]
        public bool? CallbackSuccess { get; set; }

        [JsonProperty("aq_status_msg")]
        public string AqStatusMsg { get; set; }

        [JsonProperty("amount")]
        public long? Amount { get; set; }

        [JsonProperty("acquirer")]
        public string Acquirer { get; set; }

        [JsonProperty("aq_status_code")]
        public string AqStatusCode { get; set; }

        [JsonProperty("callback_duration")]
        public int? CallbackDuration { get; set; }

        [JsonProperty("callback_at")]
        public DateTime? CallbackAt { get; set; }

        [JsonProperty("callback_response_code")]
        public string CallbackResponseCode { get; set; }

        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("callback_url")]
        public string CallbackUrl { get; set; }

        [JsonProperty("data")]
        public BrandingConfig Data { get; set; }

        [JsonProperty("qp_status_code")]
        public string QpStatusCode { get; set; }

        [JsonProperty("pending")]
        public bool? Pending { get; set; }

        [JsonProperty("qp_status_msg")]
        public string QpStatusMsg { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public partial class Link
    {
        [JsonProperty("callback_url")]
        public string CallbackUrl { get; set; }

        [JsonProperty("auto_capture")]
        public bool? AutoCapture { get; set; }

        [JsonProperty("agreement_id")]
        public long? AgreementId { get; set; }

        [JsonProperty("acquirer")]
        public object Acquirer { get; set; }

        [JsonProperty("amount")]
        public long? Amount { get; set; }

        [JsonProperty("branding_config")]
        public BrandingConfig BrandingConfig { get; set; }

        [JsonProperty("auto_fee")]
        public object AutoFee { get; set; }

        [JsonProperty("branding_id")]
        public object BrandingId { get; set; }

        [JsonProperty("deadline")]
        public object Deadline { get; set; }

        [JsonProperty("invoice_address_selection")]
        public object InvoiceAddressSelection { get; set; }

        [JsonProperty("continue_url")]
        public string ContinueUrl { get; set; }

        [JsonProperty("cancel_url")]
        public string CancelUrl { get; set; }

        [JsonProperty("customer_email")]
        public object CustomerEmail { get; set; }

        [JsonProperty("google_analytics_client_id")]
        public object GoogleAnalyticsClientId { get; set; }

        [JsonProperty("framed")]
        public bool? Framed { get; set; }

        [JsonProperty("google_analytics_tracking_id")]
        public object GoogleAnalyticsTrackingId { get; set; }

        [JsonProperty("payment_methods")]
        public object PaymentMethods { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("shipping_address_selection")]
        public object ShippingAddressSelection { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }

    public partial class Metadata
    {
        [JsonProperty("fraud_remarks")]
        public object[] FraudComments { get; set; }

        [JsonProperty("customer_country")]
        public string CustomerCountry { get; set; }

        [JsonProperty("brand")]
        public string Brand { get; set; }

        [JsonProperty("bin")]
        public string Bin { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("exp_month")]
        public long? ExpMonth { get; set; }

        [JsonProperty("customer_ip")]
        public string CustomerIp { get; set; }

        [JsonProperty("exp_year")]
        public long? ExpYear { get; set; }

        [JsonProperty("issued_to")]
        public object IssuedTo { get; set; }

        [JsonProperty("nin_number")]
        public object NinNumber { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("fraud_suspected")]
        public bool? FraudSuspected { get; set; }

        [JsonProperty("is_3d_secure")]
        public bool? Is3dSecure { get; set; }

        [JsonProperty("nin_country_code")]
        public object NinCountryCode { get; set; }

        [JsonProperty("last4")]
        public string Last4 { get; set; }

        [JsonProperty("nin_gender")]
        public object NinGender { get; set; }

        [JsonProperty("origin")]
        public string Origin { get; set; }

        [JsonProperty("reported")]
        public bool? Reported { get; set; }

        [JsonProperty("number")]
        public object Number { get; set; }

        [JsonProperty("report_description")]
        public object ReportDescription { get; set; }

        [JsonProperty("reported_at")]
        public object ReportedAt { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }


    public partial class BrandingConfig
    {
    }



}
