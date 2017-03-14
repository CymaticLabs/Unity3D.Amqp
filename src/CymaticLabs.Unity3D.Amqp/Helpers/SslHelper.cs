using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Class used for helping with SSL/TLS.
    /// </summary>
    public static class SslHelper
    {
        #region Constructor

        static SslHelper()
        {
            // Setup custom SSL validation to avoid Mono SSL issues (slightly unsecure)
            ServicePointManager.ServerCertificateValidationCallback = SslHelper.RemoteCertificateValidationCallback;
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        /// Custom SSL certificate validation callback.
        /// </summary>
        public static bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            bool isOk = true;
            // If there are errors in the certificate chain, look at each error to determine the cause.
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                for (int i = 0; i < chain.ChainStatus.Length; i++)
                {
                    if (chain.ChainStatus[i].Status != X509ChainStatusFlags.RevocationStatusUnknown)
                    {
                        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                        chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                        bool chainIsValid = chain.Build(new X509Certificate2(certificate));
                        if (!chainIsValid)
                        {
                            isOk = false;
                        }
                    }
                }
            }
            return isOk;
        }

        #endregion Methods
    }
}
