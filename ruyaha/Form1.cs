using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace ruyaha
{


    //************************************ Main program code************************************

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

     
        /// <summary>
        /// CONST definition
        /// </summary>
        
        //RUI URLs 
        private string authUrl = "https://api.revulytics.com/auth/login";
        private string logoffurl = "https://api.revulytics.com/auth/logout";
        private string featureuUsageURL = "https://api.revulytics.com/reporting/eventTracking/advanced/fullReport";
        private string reportUrlPart1 = "https://analytics.revulytics.com/dashboard/events_full?page_id=2936724257118231&json_filters=%7B%7D&date_split=day&old_stop_date=";
        private string reportUrlPart2 = @"&product_id=";
        //Aha URLs
        string featuresURL = "https://secure.aha.io/api/v1/releases/"; // to concatenate with the project id and "/features/
        string ahaFeatureDetailsURL= "https://secure.aha.io/api/v1/features/"; //to concatenate with the feature ID.
        


   //----------------------------------------------------------------------------------
        /// <summary>
        /// Function to enumerate the Aha! features for a given project
        /// </summary>
        /// <param name="url">
        /// Takes as input the url which encodes the project to retrieve features from
        /// </param>
        /// <returns>
        /// Returns the deserialized JSON AhaEnumRootObject which is a list of Aha! features (in brief)
        /// </returns>
        private AhaEnumRootObject GetAhaFeatures(string url)

        {
            WebClient client = new WebClient();
            InitializeAhaWebClient(ref client);
            try
            {
                var result = client.DownloadString(url);
                AhaEnumRootObject desRoot = new AhaEnumRootObject();
                EnumFeature desfeature = new EnumFeature();
                EnumPagination dspagination = new EnumPagination();
                desRoot = JsonConvert.DeserializeObject<AhaEnumRootObject>(result);
                return desRoot;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return null;
            }
        }

  //----------------------------------------------------------------------------------
        /// <summary>
        /// Gets the entire array of Aha! feature attributes for a specified feature
        /// </summary>
        /// <param name="feature">
        /// This is Feature object retrieved when enumerating the Aha! features. The reference umber is used to build the url for the request
        /// </param>
        /// <returns>
        /// The function returns a deserialized JSON containig all the feature attributes and values
        /// </returns>
        private FeatureDetailRoorObject GetAhaFeatureDetails(EnumFeature feature)
        {
           
            WebClient client = new WebClient();
            InitializeAhaWebClient(ref client);
            string url = ahaFeatureDetailsURL + feature.reference_num;
            try
            {
                var result = client.DownloadString(url);
                FeatureDetailRoorObject featureRoot = new FeatureDetailRoorObject();
                Feature desfeature = new Feature();
                featureRoot = JsonConvert.DeserializeObject<FeatureDetailRoorObject>(result);
                return featureRoot;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return null;
            }

        }
 //----------------------------------------------------------------------------------    
        /// <summary>
        /// gets Aha info and places it in a listview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {

            string specificFeatureURL = featuresURL + textBox6.Text + @"/features/";
            AhaEnumRootObject enumRootObject = GetAhaFeatures(specificFeatureURL);

            for (int i = 0; i < 6; i++)
            {
                listView1.Items.Add(enumRootObject.features[i].name);
                FeatureDetailRoorObject enumDetails = GetAhaFeatureDetails(enumRootObject.features[i]);
                int cfCount = enumDetails.feature.custom_fields.Count;
                if (cfCount != 0)
                {
                    CustomField cField = new CustomField();
                    cField = enumDetails.feature.custom_fields.Find(x => x.name.Equals("Base feature"));
                    if (cField != null)
                    {
                        listView1.Items[i].SubItems.Add(cField.value.ToString());

                    }
                }
            }
            MessageBox.Show("Aha! information successfuly retrieved.");
        }

 //----------------------------------------------------------------------------------
        /// <summary>
        /// Gets RUI info and places it in a list view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {


            if (listView1.Items.Count > 0)
            {
                WebClient client = new WebClient();
                RuyAuthenticationResponse authResponseObj = new RuyAuthenticationResponse();
                authResponseObj = InitializeRUIWebClient(ref client, textBox3.Text, textBox2.Text, authUrl);

                //build events filter
                List<AdvancedEvent> list = new List<AdvancedEvent>();
                for (int j = 0; j < listView1.Items.Count; j++)
                {
                    if (listView1.Items[j].SubItems.Count > 1)
                    {
                        AdvancedEvent aEvent = new AdvancedEvent();
                        aEvent.category = "FeatureUsage";
                        aEvent.name = listView1.Items[j].SubItems[1].Text;
                        list.Add(aEvent);
                    }
                }
                //build request object

                RuiAdvancedRequest advancedRequest = new RuiAdvancedRequest();
                DateTime startDate = DateTime.Now.AddDays(-90);
                advancedRequest.user = textBox3.Text;
                advancedRequest.sessionId = authResponseObj.sessionId;
                advancedRequest.productId = Convert.ToInt64(textBox1.Text);
                advancedRequest.startDate = startDate.ToString("yyyy-MM-dd");
                advancedRequest.stopDate = DateTime.Now.Date.ToString("yyyy-MM-dd");
                // advancedRequest.globalFilters = "null";
                // advancedRequest.segmentBy = "null";
                advancedRequest.events = list;
                string serializedRequest = JsonConvert.SerializeObject(advancedRequest);
                string reportResult = "";
                //execute request
                try
                {
                    reportResult = client.UploadString(featureuUsageURL, "POST", serializedRequest);
                }
                catch (WebException erro)
                {
                    {
                        string responseText;

                        var responseStream = erro.Response?.GetResponseStream();

                        if (responseStream != null)
                        {
                            using (var reader = new StreamReader(responseStream))
                            {
                                responseText = reader.ReadToEnd();
                                //MessageBox.Show(responseText);
                            }
                        }

                    }

                }
                //deserialize response json

                DetailedReportRootObject dRR = JsonConvert.DeserializeObject<DetailedReportRootObject>(reportResult);

                // for each base feature, calculate RUI usage rate and save in a list view
                if (dRR.status == "OK")
                {

                    for (int i = 0; i < listView1.Items.Count; i++)
                    {

                        DetailedReport report = new DetailedReport();
                        if (listView1.Items[i].SubItems.Count > 1)
                        {
                            DetailedReport dReport = new DetailedReport();
                            dReport = dRR.results.detailedReport.Find(x => x.name.Equals(listView1.Items[i].SubItems[1].Text));

                            decimal rate = ((decimal)dReport.data.uniqueUsersUsedAtLeastOnce / (decimal)(dReport.data.uniqueUsersUsedAtLeastOnce + dReport.data.uniqueUsersNeverUsed)) * 100;
                            rate = Math.Round(rate, 2);
                            listView1.Items[i].SubItems.Add(rate.ToString());
                            listView1.Items[i].SubItems.Add(dReport.data.uniqueUsersUsedAtLeastOnce.ToString());
                        }
                    }

                }

                //Build RUY Logoff JSON
                RuYLogoff logoffObj = new RuYLogoff();
                logoffObj.user = textBox3.Text;
                logoffObj.sessionId = authResponseObj.sessionId;
                string logoff = JsonConvert.SerializeObject(logoffObj);
                //POST the RUY logoff request
                string logoffResult = client.UploadString(logoffurl, "POST", logoff);
            }
            else MessageBox.Show("The list is empty. Please get Aha! feature information first!");
            MessageBox.Show("Revulytics information successfully retrieved.");
        }

  //----------------------------------------------------------------------------------
      /// <summary>
      /// Initializes RUI web client
      /// </summary>
      /// <param name="client"> This is the reference to the WebClient object which is being initialized</param>
      /// <param name="user">This is the user account to authenticate</param>
      /// <param name="password">This is the password to authenticate</param>
      /// <param name="authUrl">This is the URL to use for the connection</param>
      /// <returns></returns>
        private static RuyAuthenticationResponse InitializeRUIWebClient(ref WebClient client, string user, string password, string authUrl)
        {
            client.Headers.Add("ContentType", "application/json");
            client.Headers.Add("Accept", "application/json");
            //build RUY Authentication JSON
            RuyAuthentication RuyAuthenticationRequest = new RuyAuthentication();
            RuyAuthenticationRequest.user = user;
            RuyAuthenticationRequest.password = password;
            RuyAuthenticationRequest.useCookies = false;
            string encodeJson = JsonConvert.SerializeObject(RuyAuthenticationRequest);
            string result = "";
            //POST the RUY authentication request
            result = client.UploadString(authUrl, "POST", @encodeJson);
            RuyAuthenticationResponse authResponseObj = new RuyAuthenticationResponse();
            authResponseObj = JsonConvert.DeserializeObject<RuyAuthenticationResponse>(result);
            return authResponseObj;


        }

 //----------------------------------------------------------------------------------
 /// <summary>
 /// Initializes the Aha! web client
 /// </summary>
 /// <param name="client">This is the reference to the WebClient oject which is being initialized</param>
        public void InitializeAhaWebClient(ref WebClient client)
        {
            NetworkCredential myCreds = new NetworkCredential("", "");
            myCreds.UserName = textBox4.Text;
            myCreds.Password = textBox5.Text;
            client.Credentials = myCreds;
            string account = textBox7.Text;
            client.Headers.Add("X-AHA-ACCOUNT", account);
            client.Headers.Add("Content-Type", "application/json");
            client.Headers.Add("Accept", "application/json");
        }
        public FeatureDetailRoorObject SetAhaCustomFieldValues(FeatureDetailRoorObject featureToUpdate,CustomField customFieldToUpdate, string valueToUpdate, string urlValue, string customers)
        {
            string specificFeatureURL = featuresURL + textBox6.Text + @"/features/";

           string requestURL = specificFeatureURL + featureToUpdate.feature.id.ToString();
           // CustomField cField = featureToUpdate.feature.custom_fields.Find(x => x.name.Equals(customFieldToUpdate.name));

           // if (cField != null)
           // {

                UrCustomField cFieldToSerialize = new UrCustomField();
                cFieldToSerialize.base_feature_adoption_rate = valueToUpdate;
                cFieldToSerialize.rui_url = urlValue;
                cFieldToSerialize.customers_using_base_feature = customers;
                UrFeature featureToSerialize = new UrFeature();
                featureToSerialize.custom_fields = cFieldToSerialize;

                UpdateFeatureRootObject rObj = new UpdateFeatureRootObject();
                rObj.feature = featureToSerialize;
                string reqToUpdate = JsonConvert.SerializeObject(rObj);
                WebClient client = new WebClient();
                InitializeAhaWebClient(ref client);
                string result = "";
                try { result = client.UploadString(requestURL, "PUT", reqToUpdate); }

                catch (WebException erro)
                {
                    {
                        string responseText;

                        var responseStream = erro.Response?.GetResponseStream();

                        if (responseStream != null)
                        {
                            using (var reader = new StreamReader(responseStream))
                            {
                                responseText = reader.ReadToEnd();
                                //MessageBox.Show(responseText);
                            }
                        }
                    }
                    return null;
                }
                FeatureDetailRoorObject returnResult = JsonConvert.DeserializeObject<FeatureDetailRoorObject>(result);
                return returnResult;

            //}
            //return null;

        }

        //----------------------------------------------------------------------------------
        /// <summary>
        /// Sets the Aha! custom fields values
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void button3_Click(object sender, EventArgs e)
        {


            if (listView1.Items.Count > 0)
            {
                bool found = false;
                for (int i = 0; i < listView1.Items.Count; i++)
                {
                    if (listView1.Items[i].SubItems.Count == 4)
                    {
                        found = true;
                        break;
                    }

                }

                if (found)
                {

                    WebClient client = new WebClient();
                    InitializeAhaWebClient(ref client);

                    //get features with non null base feature custom field
                    string specificFeatureURL = featuresURL + textBox6.Text + @"/features/";
                    AhaEnumRootObject enumRootObject = GetAhaFeatures(specificFeatureURL);

                    try
                    {

                        for (int i = 0; i < 6; i++)
                        {
                            FeatureDetailRoorObject enumDetails = GetAhaFeatureDetails(enumRootObject.features[i]);
                            int cfCount = enumDetails.feature.custom_fields.Count;
                            if (cfCount != 0)
                            {
                                CustomField cField = new CustomField();
                                cField = enumDetails.feature.custom_fields.Find(x => x.name.Equals("Base feature"));
                                if (cField != null)
                                {

                                    string toLook4 = cField.value.ToString();
                                    ListViewItem item = listView1.FindItemWithText(toLook4);
                                    string rate = item.SubItems[2].Text;
                                    string customers = item.SubItems[3].Text;
                                    CustomField fieldToUpdate = new CustomField();
                                    fieldToUpdate = enumDetails.feature.custom_fields.Find(x => x.name.Equals("Base feature adoption rate"));
                                    if (fieldToUpdate != null)
                                    {
                                        string urlValue = reportUrlPart1 +DateTime.Now.ToString("yyyy-MM-dd") + reportUrlPart2 + textBox1.Text;
                                        FeatureDetailRoorObject result = SetAhaCustomFieldValues(enumDetails, cField, rate, urlValue,customers);
                                    }
                                }
                            }
                        }
                        MessageBox.Show("Aha! fields updated successfully");
                    }
                    catch (Exception exception) { MessageBox.Show(exception.Message); };
                }
                else MessageBox.Show("No Revulytics information found in list, please get Revulyitcs feature usage information first.");
            }
            else { MessageBox.Show("No Aha! feature information found. Please get the Aha! feature information and then the Revulytics feature usage information."); }


            }


           

    }
}

// ************************ Aha! CLASSES USED FOR (DE)SERIALIZING JSON *****************************

//---------------------Aha! feature enumeration JSON-----------------------------------

// Aha! EnumFeature class - stores the feature objects returned by the feature enumeration JSON

public class EnumFeature
            {
                public string id { get; set; }
                public string reference_num { get; set; }
                public string name { get; set; }
                public string created_at { get; set; }
                public string url { get; set; }
                public string resource { get; set; }
            }
        // Aha! EnumPagination class - stores the pagination information for the feature enumeration JSON

        public class EnumPagination
            {
                public int total_records { get; set; }
                public int total_pages { get; set; }
                public int current_page { get; set; }
            }
        // Aha! AhaEnumRootObject class - stores the root object for the feature enumeration JSON
        public class AhaEnumRootObject
            {
                public List<EnumFeature> features { get; set; }
                public EnumPagination pagination { get; set; }
            }
   //---------------------Aha! feature details JSON------------

        public class WorkflowKind
            {
                public string id { get; set; }
                public string name { get; set; }
            }

        public class WorkflowStatus
            {
                public string id { get; set; }
                public string name { get; set; }
                public bool complete { get; set; }
            }

        public class Attachment
            {
                public string id { get; set; }
                public string download_url { get; set; }
                public string created_at { get; set; }
                public int file_size { get; set; }
                public string content_type { get; set; }
                public string file_name { get; set; }
            }

        public class Description
            {
                public string id { get; set; }
                public string body { get; set; }
                public string created_at { get; set; }
                public List<Attachment> attachments { get; set; }
            }

        public class IntegrationField
            {
                public string id { get; set; }
                public string name { get; set; }
                public string value { get; set; }
                public int integration_id { get; set; }
                public string service_name { get; set; }
                public string created_at { get; set; }
            }

        public class IntegrationField2
            {
                public string id { get; set; }
                public string name { get; set; }
                public string value { get; set; }
                public int integration_id { get; set; }
                public string service_name { get; set; }
                public string created_at { get; set; }
            }

        public class OwnerCustom
            {
                public string id { get; set; }
                public string name { get; set; }
                public string email { get; set; }
                public string created_at { get; set; }
                public string updated_at { get; set; }
            }

        public class Project
            {
                public string id { get; set; }
                public string reference_prefix { get; set; }
                public string name { get; set; }
                public bool product_line { get; set; }
                public string created_at { get; set; }
            }

        public class Release
            {
                public string id { get; set; }
                public string reference_num { get; set; }
                public string name { get; set; }
                public string start_date { get; set; }
                public string release_date { get; set; }
                public bool parking_lot { get; set; }
                public string created_at { get; set; }
                public List<IntegrationField2> integration_fields { get; set; }
                public string url { get; set; }
                public string resource { get; set; }
                public OwnerCustom owner { get; set; }
                public Project project { get; set; }
            }

        public class CreatedByUser
            {
                public string id { get; set; }
                public string name { get; set; }
                public string email { get; set; }
                public string created_at { get; set; }
                public string updated_at { get; set; }
            }

        public class AssignedToUser
            {
                public string id { get; set; }
                public string name { get; set; }
                public string email { get; set; }
                public string created_at { get; set; }
                public string updated_at { get; set; }
                public bool default_assignee { get; set; }
            }

        public class WorkflowStatus2
            {
                public string id { get; set; }
                public string name { get; set; }
                public bool complete { get; set; }
            }

        public class Description2
            {
                public string id { get; set; }
                public string body { get; set; }
                public string created_at { get; set; }
                public List<object> attachments { get; set; }
            }

        public class CreatedByUser2
            {
                public string id { get; set; }
                public string name { get; set; }
                public string email { get; set; }
                public string created_at { get; set; }
                public string updated_at { get; set; }
            }

        public class Requirement
            {
                public string id { get; set; }
                public string name { get; set; }
                public string reference_num { get; set; }
                public int position { get; set; }
                public string created_at { get; set; }
                public string updated_at { get; set; }
                public WorkflowStatus2 workflow_status { get; set; }
                public string url { get; set; }
                public string resource { get; set; }
                public Description2 description { get; set; }
                public object assigned_to_user { get; set; }
                public CreatedByUser2 created_by_user { get; set; }
                public List<object> attachments { get; set; }
                public List<object> integration_fields { get; set; }
                public int comments_count { get; set; }
            }

        public class Description3
            {
                public string id { get; set; }
                public string body { get; set; }
                public string created_at { get; set; }
                public List<object> attachments { get; set; }
            }

        public class Initiative
            {
                public string id { get; set; }
                public string name { get; set; }
                public string url { get; set; }
                public string resource { get; set; }
                public string created_at { get; set; }
                public Description3 description { get; set; }
                public List<object> integration_fields { get; set; }
            }

        public class ParentRecord
            {
                public string id { get; set; }
                public string reference_num { get; set; }
                public string name { get; set; }
                public string created_at { get; set; }
                public string url { get; set; }
                public string resource { get; set; }
            }

        public class ChildRecord
            {
                public string id { get; set; }
                public string reference_num { get; set; }
                public string name { get; set; }
                public string created_at { get; set; }
                public string url { get; set; }
                public string resource { get; set; }
            }

        public class FeatureLink
            {
                public string link_type { get; set; }
                public int link_type_id { get; set; }
                public string created_at { get; set; }
                public ParentRecord parent_record { get; set; }
                public ChildRecord child_record { get; set; }
            }
        public class CustomField
            {
                public string key { get; set; }
                public string name { get; set; }
                public string value { get; set; }
                public string type { get; set; }

            }
        public class Feature
            {
                public string id { get; set; }
                public string name { get; set; }
                public string reference_num { get; set; }
                public int position { get; set; }
                public int score { get; set; }
                public string created_at { get; set; }
                public string updated_at { get; set; }
                public object start_date { get; set; }
                public object due_date { get; set; }
                public WorkflowKind workflow_kind { get; set; }
                public WorkflowStatus workflow_status { get; set; }
                public Description description { get; set; }
                public List<object> attachments { get; set; }
                public List<IntegrationField> integration_fields { get; set; }
                public string url { get; set; }
                public string resource { get; set; }
                public Release release { get; set; }
                public CreatedByUser created_by_user { get; set; }
                public AssignedToUser assigned_to_user { get; set; }
                public List<Requirement> requirements { get; set; }
                public Initiative initiative { get; set; }
                public List<object> goals { get; set; }
                public int comments_count { get; set; }
                public List<object> score_facts { get; set; }
                public List<string> tags { get; set; }
                public List<CustomField> custom_fields { get; set; }
                public List<FeatureLink> feature_links { get; set; }
            }
      
        public class FeatureDetailRoorObject
            {
                public Feature feature { get; set; }
            }
    // classes for composing JSON for updating custom fields in AHA features

        public class UrCustomField //custom fields to update
            {
                public string base_feature_adoption_rate { get; set; }
                public string rui_url { get; set; }
                public string customers_using_base_feature { get; set; }
            }
        public class UrFeature
            {
                public UrCustomField custom_fields { get; set; }
            }

        public class UpdateFeatureRootObject
            {
                public UrFeature feature { get; set; }
            }


    // ************************ Revulytics CLASSES USED FOR (DE)SERIALIZING JSON *****************************

    //--------------------------RUI functionality---------------------------



    // Class for RUI Authentication POST JSON

    public class RuyAuthentication //for RUY authentication
            {
                public string user { get; set; }
                public string password { get; set; }
                public bool useCookies { get; set; }
            }

        //Class for RUI Authentication response JSON

        public class RuyAuthenticationResponse
            {
                public string status { get; set; }
                public string sessionId { get; set; }

            }
        
        //Class for RUI logoff POST JSON

        public class RuYLogoff
            {
                public string user { set; get; }
                public string sessionId { set; get; }
            }

        //Classes for parsing JSON when enumerating RUI advanced features

        public class AdvancedEvent
            {
                public string category { get; set; }
                public string name { get; set; }
            }
        public class RuiAdvancedRequest
            {

                public string user { get; set; }
                public string sessionId { get; set; }
                public long productId { get; set; }
                public string startDate { get; set; }
                public string stopDate { get; set; }
                public List<AdvancedEvent> events { get; set; }
             }

        // classes for parsing JSON when retreiving advanced RUI report

        public class UsageFrequency
            {
                public int interval1 { get; set; }
                public int interval2 { get; set; }
                public int interval3 { get; set; }
                public int interval4 { get; set; }
                public int interval5 { get; set; }
                public int interval6 { get; set; }
                public int interval7 { get; set; }
                public int interval8 { get; set; }
                public int interval9 { get; set; }
                public int interval10 { get; set; }
                public int interval11 { get; set; }
                public int interval12 { get; set; }
                public int interval13 { get; set; }
                public int interval14 { get; set; }
                public int interval15 { get; set; }
                public int interval16 { get; set; }
                public int interval17 { get; set; }
                public int interval18 { get; set; }
                public int interval19 { get; set; }
                public int interval20 { get; set; }
                public int interval21 { get; set; }
                public int interval22 { get; set; }
            }
        public class Data
            {
                public int eventCounts { get; set; }
                public double averageEventCountPerSessionUsedAtLeastOnce { get; set; }
                public double averageEventCountPerSessionAll { get; set; }
                public double averageEventCountPerUserAtLeastOnce { get; set; }
                public double averageEventCountPerUserAll { get; set; }
                public int uniqueUsersUsedAtLeastOnce { get; set; }
                public int uniqueUsersNeverUsed { get; set; }
                public UsageFrequency usageFrequency { get; set; }
            }

        public class DetailedReport
            {
                public string category { get; set; }
                public string name { get; set; }
                public Data data { get; set; }
            }

        public class Results
            {
                public List<DetailedReport> detailedReport { get; set; }
            }

        public class DetailedReportRootObject
            {
                public string status { get; set; }
                public object segmentBy { get; set; }
                public Results results { get; set; }
            }



