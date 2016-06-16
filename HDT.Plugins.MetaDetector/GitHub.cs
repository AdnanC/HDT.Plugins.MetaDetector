using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HDT.Plugins.MetaDetector.Logging;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HDT.Plugins.MetaDetector
{
    public class GitHub
    {
        // Check if there is a newer release on Github than current
        public static async Task<GithubRelease> CheckForUpdate(string user, string repo, Version version)
        {
            try
            {
                var latest = await GetLatestRelease(user, repo);

                // tag needs to be in strict version format: e.g. 0.0.0
                latest.tag_name = latest.tag_name.Replace("v", "");
                
                Version v = new Version(latest.tag_name);

                // check if latest is newer than current
                if (v.CompareTo(version) > 0)
                {
                    return latest;
                }
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
            return null;
        }

        // Use the Github API to get the latest release for a repo
        public static async Task<GithubRelease> GetLatestRelease(string user, string repo)
        {
            var url = String.Format("https://api.github.com/repos/{0}/{1}/releases", user, repo);
            try
            {
                var json = "";
                using (WebClient wc = new WebClient())
                {
                    // API requires user-agent string, user name or app name preferred
                    wc.Headers.Add(HttpRequestHeader.UserAgent, user);
                    json = await wc.DownloadStringTaskAsync(url);
                }

                byte[] byteArray = Encoding.UTF8.GetBytes(json);
                MemoryStream stream = new MemoryStream(byteArray);

                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<GithubRelease>));
                var releases = (List<GithubRelease>)serializer.ReadObject(stream);
                return releases.FirstOrDefault<GithubRelease>();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        // Basic release info for JSON deserialization
        public class GithubRelease
        {
            public string html_url { get; set; }
            public string tag_name { get; set; }
            public string prerelease { get; set; }
            public string published_at { get; set; }
        }

    }
}