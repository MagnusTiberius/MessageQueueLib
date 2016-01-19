using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Queue;

using System.ServiceModel;
using System.ServiceModel.Channels;

using Microsoft.WindowsAzure.CAT.ServiceBusExplorer;

using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Threading;

namespace E2MS.SupportingClasses
{
    public enum BodyType
    {
        Stream,
        String,
        Wcf
    }


    public class MessageQueueHandler
    {
        private string serviceBusConnectionString;
        private string queueName;
        private QueueClient client;
        private JsonSerializer serializer;
        NamespaceManager namespaceManager;
        public string messageID;
        private const int MaxBufferSize = 262144; // 256 KB
        private const string UnableToReadMessageBody = "Unable to read the message body.";

        private JsonSerializerSettings settings = null;

        public delegate void OnMessage(string message);
        public event OnMessage onMessage;

        public delegate void OnMessageWithId(string message, string messageId);
        public event OnMessageWithId onMessageWithId;

        public delegate void OnMessageWithBM(string message, BrokeredMessage brokeredMessage);
        public event OnMessageWithBM onMessageWithBM;

        public string QueueName 
        {
            get
            {
                return queueName;
            }
        }

        public string ServiceBusConnectionString
        {
            get
            {
                return serviceBusConnectionString;
            }
        }

        public string MessageID
        {
            get
            {
                return messageID;
            }
        }

        public MessageQueueHandler(string connectString, string queueNameString, object data)
        {
            settings = new JsonSerializerSettings();
            settings.NullValueHandling = NullValueHandling.Ignore;
            serviceBusConnectionString = connectString;
            queueName = queueNameString;
            client = QueueClient.CreateFromConnectionString(serviceBusConnectionString, queueNameString);
            serializer = new JsonSerializer()
            {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ContractResolver = new JsonPrivateSetterPropertyContractResolver(),
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            };

            serializer.Converters.Add(new IsoDateTimeConverter());

            try
            {
                namespaceManager = NamespaceManager.CreateFromConnectionString(serviceBusConnectionString);
                if (!namespaceManager.QueueExists(queueName))
                    namespaceManager.CreateQueue(queueName);

            }
            catch (Exception)
            {

            }
        }

        public MessageQueueHandler(string erpOrThunder, string messageType)
        {
            settings = new JsonSerializerSettings();
            settings.NullValueHandling = NullValueHandling.Ignore;

            SetQueueConnectionData(erpOrThunder, messageType);

            client = QueueClient.CreateFromConnectionString(serviceBusConnectionString, queueName);

            serializer = new JsonSerializer()
            {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ContractResolver = new JsonPrivateSetterPropertyContractResolver(),
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            };

            serializer.Converters.Add(new IsoDateTimeConverter());

            try
            {
                namespaceManager = NamespaceManager.CreateFromConnectionString(serviceBusConnectionString);
                if (!namespaceManager.QueueExists(queueName))
                    namespaceManager.CreateQueue(queueName);

            }
            catch (Exception)
            {

            }
        }

        private void SetQueueConnectionData(string environment, string messageType)
        {
            return;
        }

        public string SendMessage(object o, out Exception e)
        {
            try
            {
                string jsonString = SendMessage(o);
                e = null;
                return (jsonString);
            }
            catch (Exception ex)
            {
                e = ex;
                return null;
            }
        }

        public string SendMessage(object o)
        {
            string jsonString = JsonConvert.SerializeObject(o, settings);
            BrokeredMessage message = new BrokeredMessage(jsonString);
            messageID = message.MessageId;
            client.Send(new BrokeredMessage(jsonString));
            return (jsonString);
        }

        public BrokeredMessage SendMessageBM(object o)
        {
            string jsonString = JsonConvert.SerializeObject(o, settings);
            BrokeredMessage message = new BrokeredMessage(jsonString);
            messageID = message.MessageId;
            client.Send(new BrokeredMessage(jsonString));
            return (message);
        }


        public void ReceiveMessage<T>(out T obj)
        {
            // Configure the callback options
            //OnMessageOptions options = new OnMessageOptions();
            //options.AutoComplete = false;
            //options.AutoRenewTimeout = TimeSpan.FromMinutes(1);
            BrokeredMessage receivedMessage = client.Receive();
            string sval = receivedMessage.ToString();
            BodyType bodyType;
            string body = GetMessageText(receivedMessage, out bodyType);
            obj = JsonConvert.DeserializeObject<T>(body);
            receivedMessage.DeadLetter();
        }

        public void Listen()
        {

            string body = null;
            // Configure the callback options
            OnMessageOptions options = new OnMessageOptions();
            options.AutoComplete = false;
            options.AutoRenewTimeout = TimeSpan.FromMinutes(60);
            //options.MaxConcurrentCalls = 1;

            //BrokeredMessage receivedMessage = client.Receive(TimeSpan.FromMinutes(2));

            BodyType bodyType;

            //body = GetMessageText(receivedMessage, out bodyType);
            //receivedMessage.DeadLetter();

            //body = receivedMessage.GetBody<string>();
            // Callback to handle received messages
            client.OnMessage((msg) =>
            {
                try
                {
                    // Process message from queue
                    //body = message.GetBody<string>();
                    //string messageID = message.MessageId;
                    body = GetMessageText(msg, out bodyType);
                    // Pass message to event handler.
                    onMessage(body);
                    // Remove message from queue
                    msg.DeadLetter();  //Complete();
                }
                catch (Exception ex)
                {
                    // Indicates a problem, unlock message in queue
                    System.Diagnostics.Debug.WriteLine(string.Format("Error Encountered: {0}", ex.Message));
                    msg.Abandon();
                }
            });
            return;
        }

        public void ProcessMessages()
        {
            string body = null;
            // Configure the callback options
            OnMessageOptions options = new OnMessageOptions();
            options.AutoComplete = false;
            options.AutoRenewTimeout = TimeSpan.FromMinutes(1);
            BodyType bodyType;

            //BrokeredMessage receivedMessage = client.Receive(TimeSpan.FromMinutes(1));
            BrokeredMessage receivedMessage = client.Receive();
            while (receivedMessage != null)
            {
                body = GetMessageText(receivedMessage, out bodyType);
                if (onMessage != null)
                {
                    onMessage(body);
                    receivedMessage.DeadLetter();
                }
                if (onMessageWithId != null)
                {
                    onMessageWithId(body, receivedMessage.MessageId);
                    receivedMessage.DeadLetter();
                }
                if (onMessageWithBM != null)
                {
                    onMessageWithBM(body, receivedMessage);
                }
                Thread.Sleep(50);
                //receivedMessage = client.Receive(TimeSpan.FromMinutes(2));
                receivedMessage = client.Receive();
            }
        }

        public string ReceiveMessage()
        {
            string body = null;
            // Configure the callback options
            OnMessageOptions options = new OnMessageOptions();
            options.AutoComplete = false;
            options.AutoRenewTimeout = TimeSpan.FromMinutes(1);

            BrokeredMessage receivedMessage = client.Receive(TimeSpan.FromMinutes(2));

            BodyType bodyType;

            body = GetMessageText(receivedMessage, out bodyType);
            receivedMessage.DeadLetter();
            //body = receivedMessage.GetBody<string>();
            // Callback to handle received messages
            //client.OnMessage((message) =>
            //{
            //    try
            //    {
            //        // Process message from queue
            //        body = message.GetBody<string>();
            //        string messageID = message.MessageId;

            //        // Remove message from queue
            //        message.Complete();
            //    }
            //    catch (Exception)
            //    {
            //        // Indicates a problem, unlock message in queue
            //        message.Abandon();
            //    }
            //});
            return body;
        }

        public void ReceiveMessages()
        {
            client.OnMessage((receivedMessage) =>
            {
                try
                {
                    // Process message from queue
                    BodyType bodyType;
                    GetMessageText(receivedMessage, out bodyType);
                    string messageID = receivedMessage.MessageId;
                    // Remove message from queue
                    receivedMessage.Complete();
                }
                catch (Exception)
                {
                    // Indicates a problem, unlock message in queue
                    receivedMessage.Abandon();
                }
            });
        }

        /// <summary>
        /// Reads the content of the BrokeredMessage passed as argument.
        /// </summary>
        /// <param name="messageToRead">The BrokeredMessage to read.</param>
        /// <param name="bodyType">BodyType</param>
        /// <returns>The content of the BrokeredMessage.</returns>
        public string GetMessageText(BrokeredMessage messageToRead, out BodyType bodyType)
        {
            string messageText = null;
            Stream stream = null;
            bodyType = BodyType.Stream;
            if (messageToRead == null)
            {
                return null;
            }
            var inboundMessage = messageToRead.Clone();
            try
            {
                stream = inboundMessage.GetBody<Stream>();
                if (stream != null)
                {
                    var element = new BinaryMessageEncodingBindingElement
                    {
                        ReaderQuotas = new XmlDictionaryReaderQuotas
                        {
                            MaxArrayLength = int.MaxValue,
                            MaxBytesPerRead = int.MaxValue,
                            MaxDepth = int.MaxValue,
                            MaxNameTableCharCount = int.MaxValue,
                            MaxStringContentLength = int.MaxValue
                        }
                    };
                    var encoderFactory = element.CreateMessageEncoderFactory();
                    var encoder = encoderFactory.Encoder;
                    var stringBuilder = new StringBuilder();
                    var message = encoder.ReadMessage(stream, MaxBufferSize);
                    using (var reader = message.GetReaderAtBodyContents())
                    {
                        // The XmlWriter is used just to indent the XML message
                        var settings = new XmlWriterSettings { Indent = true };
                        using (var writer = XmlWriter.Create(stringBuilder, settings))
                        {
                            writer.WriteNode(reader, true);
                        }
                    }
                    messageText = stringBuilder.ToString();
                    bodyType = BodyType.Wcf;
                }
            }
            catch (Exception)
            {
                inboundMessage = messageToRead.Clone();
                try
                {
                    stream = inboundMessage.GetBody<Stream>();
                    if (stream != null)
                    {
                        var element = new BinaryMessageEncodingBindingElement
                        {
                            ReaderQuotas = new XmlDictionaryReaderQuotas
                            {
                                MaxArrayLength = int.MaxValue,
                                MaxBytesPerRead = int.MaxValue,
                                MaxDepth = int.MaxValue,
                                MaxNameTableCharCount = int.MaxValue,
                                MaxStringContentLength = int.MaxValue
                            }
                        };
                        var encoderFactory = element.CreateMessageEncoderFactory();
                        var encoder = encoderFactory.Encoder;
                        var message = encoder.ReadMessage(stream, MaxBufferSize);
                        using (var reader = message.GetReaderAtBodyContents())
                        {
                            messageText = reader.ReadString();
                        }
                        bodyType = BodyType.Wcf;
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        if (stream != null)
                        {
                            try
                            {
                                stream.Seek(0, SeekOrigin.Begin);
                                var serializer = new CustomDataContractBinarySerializer(typeof(string));
                                messageText = serializer.ReadObject(stream) as string;
                                bodyType = BodyType.String;
                            }
                            catch (Exception)
                            {
                                try
                                {
                                    stream.Seek(0, SeekOrigin.Begin);
                                    using (var reader = new StreamReader(stream))
                                    {
                                        messageText = reader.ReadToEnd();
                                        if (messageText.ToCharArray().GroupBy(c => c).
                                            Where(g => char.IsControl(g.Key) && g.Key != '\t' && g.Key != '\n' && g.Key != '\r').
                                            Select(g => g.First()).Any())
                                        {
                                            stream.Seek(0, SeekOrigin.Begin);
                                            using (var binaryReader = new BinaryReader(stream))
                                            {
                                                var bytes = binaryReader.ReadBytes((int)stream.Length);
                                                messageText = BitConverter.ToString(bytes).Replace('-', ' ');
                                            }
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    messageText = UnableToReadMessageBody;
                                }
                            }
                        }
                        else
                        {
                            messageText = UnableToReadMessageBody;
                        }
                    }
                    catch (Exception)
                    {
                        messageText = UnableToReadMessageBody;
                    }
                }
            }
            return messageText;
        }


        private class JsonPrivateSetterPropertyContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var prop = base.CreateProperty(member, memberSerialization);

                if (!prop.Writable)
                {
                    var property = member as PropertyInfo;
                    if (property != null)
                    {
                        var hasPrivateSetter = property.GetSetMethod(true) != null;
                        prop.Writable = hasPrivateSetter;
                    }
                }

                return prop;
            }
        }
    }
}