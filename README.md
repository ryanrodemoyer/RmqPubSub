# RabbitMQ Publish and Subscribe (RmqPubSub)
===

## Description
The purpose of this tool is to have one application that can act as a message producer (publisher) and as a long-running message consumer (subscriber). The ultimate goal was to verify behavior of a RabbitMQ cluster that is sitting behind a load balancer.

Multiple consumers can get created in the same console window or spread them over multiple console windows. Each consumer will identify itself it if prints any information to the console window.

## Nerd About
RmqPubSub is a C# console app that targets .NET 4.5. We reference the following packages:

* CommandLineParser -> provide useful commands to the console app
* Costura.Fody -> bundle dll references as embedded resources to keep the output to a single executable
* Newtonsoft.Json -> JSON manipulation
* RabbitMQ.Client -> communicate with RabbitMQ

## Example Commands

* Commands publish and consume support the `--debug` flag which will attach a debugger at the entry point of the command.
* `--help` is supported and gives detailed information directly in the console.

`rmqpubsub.exe publish --server <SERVER_ADDRESS> --vhost <VHOST_NAME> --exchange <EXCHANGE_NAME> --queue <QUEUE_NAME> --username <RABBITMQ_USERNAME> --password <RABBITMQ_PASSWORD> --message <MESSAGE> --times <TIMES>`

<SERVER_ADDRESS> -> name or ip address of the load balancer or specific node
<VHOST_NAME> -> name of the virtual host in RabbitMQ
<EXCHANGE> -> name of the exchange in RabbitMQ
<QUEUE> -> name of the queue in RabbitMQ
<RABBITMQ_USERNAME> -> the username of the RabbitMQ user
<RABBITMQ_PASSWORD> -> the password of the RabbitMQ user
<MESSAGE> -> the data to send in the message
<TIMES> -> the amount of times to send the message, default is 1

`rmqpubsub.exe consume --server <SERVER_ADDRESS> --vhost <VHOST_NAME> --queue <QUEUE_NAME> --username <RABBITMQ_USERNAME> --password <RABBITMQ_PASSWORD>  --howmany <HOW_MANY>`

<SERVER_ADDRESS> -> name or ip address of the load balancer or specific node
<VHOST_NAME> -> name of the virtual host in RabbitMQ
<QUEUE> -> name of the queue in RabbitMQ
<RABBITMQ_USERNAME> -> the username of the RabbitMQ user
<RABBITMQ_PASSWORD> -> the password of the RabbitMQ user
<HOW_MANY> -> how many consumers to create, default is 1
