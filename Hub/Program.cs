﻿using log4net;
using log4net.Config;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utils;
namespace Hub
{
    class Program
    {
        private static ClientAMQP _amqpconn;
        private static Config _config;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static void Main()
        {

            try
            {
                // Inizializzazione configurazione Log4Net
                var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
                XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
             
                //Inizializzazione configurazione 
                _config = Utils.Utils.ReadConfiguration();

                //Inizializzazione modulo di monitoring
                Modulo modulo = _config.Monitoring.Modules.Find(x => x.Name.Contains("Hub"));
                AliveServer(modulo.Ip, modulo.Port);

                //Inizializzazione client AMQP 
                _amqpconn = new ClientAMQP();
                _amqpconn.CreateExchange(_config.Communications.AMQP.Exchange, ExchangeType.Direct.ToString());

                //Inizializzazione server MQTT e ricezione messaggi     
                ServerMQTT mqttC = new ServerMQTT();
                mqttC.MessageReceived += OnMQTTMessageReceived;
                mqttC.StartAsync();
                mqttC.ReceiveAsync();

                _log.Info("HUB INIZIALIZZATO CORRETTAMENTE!");

                Console.ReadLine();
                                 
            }
            catch (Exception e)
            {
                _log.ErrorFormat("!ERROR: {0}", e.ToString());
            }            
        }


        /// <summary>
        /// Metodo invocato ogni qual volta viene ricevuto un messaggio MQTT
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void OnMQTTMessageReceived(object sender, string e)
        {            
            try
            {
                //Pubblicazione messaggio ricevuto su broker AMQP 
               
                var message = new AMQPMessage { Data = e, Type = AMQPMessageType.Telemetry, Sender = _config.Communications.AMQP.Queue };
                var json = JsonConvert.SerializeObject(message);
                await _amqpconn.SendMessageAsync(_config.Communications.AMQP.Exchange, "common" ,json);             
                
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("!ERROR: {0}", ex.ToString());
            }                  
        }

        /// <summary>
        /// Ping microservizio (monitoring)
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        private static async void AliveServer(string ip, int port)
        {
            try
            {
                await Task.Run(() =>
                {
                    PingServer server = new PingServer(ip, port);
                });
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("!ERROR: {0}", ex.ToString());
            }            
        }
    }
}
