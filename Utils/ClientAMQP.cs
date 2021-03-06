﻿using log4net;
using log4net.Config;
using MQTTnet;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Utils
{
    public enum EXCHANGE_TYPE
    {

    }
    public class ClientAMQP
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly String queue;

        public EventHandler<String> AMQPMessageReceived;
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);



        public ClientAMQP()
        {
            try
            {
                var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
                XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

                Config config = Utils.ReadConfiguration();
                var factory = new ConnectionFactory();
                factory.DispatchConsumersAsync = true;
                factory.UserName = config.Communications.AMQP.UserName;
                factory.Password = config.Communications.AMQP.Password;
                factory.HostName = config.Communications.AMQP.Ip;
                factory.Port = config.Communications.AMQP.Port;
                factory.AutomaticRecoveryEnabled = true;
                factory.VirtualHost = config.Communications.AMQP.VirtualHost;
                //inizializzazione connessione e canale di comunicazione
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
            }
            catch (Exception e)
            {
                log.Error($"ERROR: {e.Message}");
            }

        }

        ~ClientAMQP()
        {
            Close();
        }

        protected virtual void OnAMQPMessageReceived(String message)
        {
            try
            {
                if (AMQPMessageReceived != null)
                    AMQPMessageReceived(this, message);
            }
            catch (Exception e)
            {
                log.Error($"ERROR: {e.Message}");
            }
        }

        public void CreateExchange(String exchangeName, String type)
        {
            try
            {
                _channel.ExchangeDeclare(exchange: exchangeName, type: type);
            }
            catch (Exception e)
            {
                log.Error($"ERROR: {e.Message}");
            }
        }

        public void CreateQueue(String queueName)
        {
            try
            {
                if (_channel != null && _channel.IsOpen)
                {
                    //dichiarazione coda
                    _channel.QueueDeclare(queue: queueName,
                                            durable: false,
                                            exclusive: false,
                                            autoDelete: true,
                                            arguments: null);
                }
            }
            catch (Exception e)
            {
                log.Error($"ERROR: {e.Message}");
            }
                      
        }

        public void BindQueue(String queueName, String exchangeName, String routingKey)
        {
            //associazione exhange a coda
            try
            {
                _channel.QueueBind(queue: queueName,
              exchange: exchangeName,
              routingKey: routingKey);
            }
            catch(Exception e)
            {
                log.Error($"ERROR: {e.Message}");
            }
        }

           
        public void Close()
        {
            try
            {
                if (_channel.IsClosed) return;

                _channel.Close();
                _connection.Close();
            }
            catch (Exception e)
            {
                log.Error($"ERROR: {e.Message}");
            }
        }

        public async Task SendMessageAsync(String exchange, String routingKey, String message)
        {
            try
            {
                await Task.Run(() =>
                {
                     
                    if (String.IsNullOrEmpty(message)) throw new ArgumentNullException();

                    //invio             
                    var body = Encoding.UTF8.GetBytes(message);
                    lock (_channel)
                    {
                        _channel.BasicPublish(exchange: exchange,
                                         routingKey: routingKey,
                                         basicProperties: null,
                                         body: body);
                    }               
                                        
                    log.InfoFormat("+MESSAGE-SEND: PAYLOAD: {0}", message);
                });
            }
            
            catch (Exception e)
            {
               log.ErrorFormat("!ERROR: {0}", e.ToString());
            }
        }


        public async Task SendMessageAsync(String exchange, String routingKey, String message, IModel chan)
        {
            try
            {
                await Task.Run(() =>
                {

                    if (String.IsNullOrEmpty(message)) throw new ArgumentNullException();

                    //se non è stato specificato alcun canale, allora utilizza quello definito implicitamente nell'istanza della classe
                    if (chan == null) chan = _channel;


                    //invio            
                    var body = Encoding.UTF8.GetBytes(message);
                    lock (chan)
                    {
                        chan.BasicPublish(exchange: exchange,
                                         routingKey: routingKey,
                                         basicProperties: null,
                                         body: body);
                    }


                    log.InfoFormat("+MESSAGE-SEND: PAYLOAD: {0}", message);
                });
            }

            catch (Exception e)
            {
                log.ErrorFormat("!ERROR: {0}", e.ToString());
            }
        }

        public IModel CreateChannel()
        {
            return _connection.CreateModel();
        }

        public void CloseChannel(IModel chan)
        {      
            chan.Close();
        }

        public void ReceiveMessageAsync(string queueName)
        {
            try
            {
                var consumer = new AsyncEventingBasicConsumer(_channel);
                String message = "";
                consumer.Received += async (model, ea) =>
                {                    
                    var body = ea.Body;                    
                    message = Encoding.UTF8.GetString(body.ToArray());                  
                    log.InfoFormat("+MESSAGE-READ: PAYLOAD: {0}", message);
                    OnAMQPMessageReceived(message);
                    await Task.Yield();
                };
                lock (_channel)
                {
                    _channel.BasicConsume(queue: queueName,
                                         autoAck: true,
                                         consumer: consumer);
                }
            }
            catch (Exception e)
            {
                log.ErrorFormat("!ERROR: {0}", e.ToString());
            }       
        }
    }
}
