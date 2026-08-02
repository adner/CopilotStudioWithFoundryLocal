

# Copilot Studio con Foundry Local

Esta solución te permite conectar Microsoft Copilot Studio a modelos de LLM locales que se ejecutan en tu máquina a través de Azure Relay. En lugar de llamar a modelos basados en la nube, tu copiloto puede enrutar las solicitudes a LLMs alojados localmente.

## Cómo funciona

La configuración utiliza Azure Relay como un puente entre los recursos en la nube y los locales:

1. **RelayClient** (Azure Function) - Desplegado en Azure, recibe solicitudes de Copilot Studio y las reenvía a través de Azure Relay
2. **LocalRelayServer** - Se ejecuta localmente, escucha las solicitudes a través de Azure Relay y las enruta a los clientes de LLM locales
3. **LocalClients** - Biblioteca envoltorio para comunicarse con los servidores de inferencia de LLM locales

## ¿Por qué dos clientes de LLM diferentes?

La solución utiliza tanto **FoundryLocalClient** como **LMStudioClient** por una razón específica: **Foundry Local actualmente no admite las llamadas a herramientas/funciones al estilo de OpenAI** cuando se utiliza su punto final compatible con OpenAI.

Según [este hilo](https://learn.microsoft.com/en-gb/answers/questions/2281875/local-foundry-tool-calling-with-openai-client), la API de Foundry Local solo es compatible con "puntos finales de completado de chat básico, incrustación y completado, no con llamadas a funciones". Esto, por supuesto, cambiará en el futuro a medida que Foundry Local evolucione.

Para tareas que requieren llamadas a herramientas, como interactuar con el equipo local y llamar al SDK de Foundry Local para cargar modelos, etc., esta solución enruta esas solicitudes a **LM Studio**, que admite la especificación de llamadas a funciones de OpenAI. Los completados simples que no necesitan herramientas se envían a **Foundry Local**.

## Proyectos

### LocalClients
Biblioteca principal con clientes compatibles con OpenAI para modelos locales:
- **FoundryLocalClient** - Utiliza Microsoft AI Foundry Local para ejecutar modelos localmente (solo completados básicos)
- **LMStudioClient** - Se conecta a LM Studio con soporte completo para llamadas a herramientas/funciones
- **LocalOpenAiClient** - Clase base para puntos finales compatibles con OpenAI

### LocalRelayServer
Aplicación de consola que mantiene una conexión persistente con Azure Relay y enruta las solicitudes entrantes:
- Solicitudes `AdminTask` → LMStudioClient (orquestación/planificación con llamadas a herramientas)
- Solicitudes `ChatCompletion` → FoundryLocalClient (completados simples)

### RelayClient
Azure Function que actúa como el retransmisor en el lado de la nube:
- Acepta solicitudes HTTP de Copilot Studio
- Las reenvía a través de Azure Relay a tu máquina local
- Devuelve la respuesta del LLM a Copilot Studio

## Configuración

### Requisitos previos
- Suscripción de Azure (para Azure Relay y Azure Functions)
- LM Studio o un servidor compatible con OpenAI ejecutándose localmente
- .NET 9.0

### Configuración

Crea un espacio de nombres de Azure Relay y configura una Conexión Híbrida, tal como se describe [aquí](https://learn.microsoft.com/en-us/azure/azure-relay/relay-hybrid-connections-http-requests-dotnet-get-started). Toma nota del espacio de nombres del retransmisor, el nombre, el nombre de la clave SAS y la clave, los cuales deben agregarse a los archivos de configuración.

Crea estos archivos de configuración (están ignorados por git):

**LocalRelayServer/appsettings.Development.json:**
```json
{
  "AzureRelay": {
    "RelayNamespace": "your-namespace.servicebus.windows.net",
    "ConnectionName": "your-connection-name",
    "KeyName": "RootManageSharedAccessKey",
    "Key": "your-key-here"
  }
}
```

**RelayClient/local.settings.json:**
```json
{
  "Values": {
    "AzureRelay:RelayNamespace": "your-namespace.servicebus.windows.net",
    "AzureRelay:ConnectionName": "your-connection-name",
    "AzureRelay:KeyName": "RootManageSharedAccessKey",
    "AzureRelay:Key": "your-key-here"
  }
}
```

### Ejecución local

1. Inicia tu servidor de LLM local (LM Studio en el puerto 1234)
2. Ejecuta LocalRelayServer: `dotnet run --project LocalRelayServer`
3. Despliega RelayClient en Azure Functions
4. Configura Copilot Studio para que llame al punto final de tu Azure Function

## ¿Por qué esta configuración?

Copilot Studio no puede acceder directamente a tu máquina local, pero Azure Relay crea un túnel seguro. Tu servidor local mantiene una conexión saliente hacia Azure, por lo que no se necesitan reglas de firewall entrantes. Cuando Copilot Studio necesita un modelo local, la solicitud fluye a través de Azure Relay hacia tu máquina y vuelve.
