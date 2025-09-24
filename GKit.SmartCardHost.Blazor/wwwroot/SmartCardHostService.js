import * as signalR from "./signalr";

export const cardReaderService = {
    proxy: null,
};

export const cardReaderConnection = ((service) => {
    const obj = {
        releaseLock: () => {
        },
        getConnectionState: null,
        start: null,
        stop: null,
    };

    let shouldBeConnected = false;

    if (navigator && navigator.locks && navigator.locks.request) {
        navigator.locks.request("card_reader_lock", {mode: "shared"}, (lock) => {
            return new Promise((resolve) => {
                obj.releaseLock = resolve;
            });
        });
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("http://127.0.0.1:23231/card-reader", {
            skipNegotiation: true,
            transport: signalR.HttpTransportType.WebSockets,
        })
        .configureLogging(signalR.LogLevel.Debug) //TODO: rimuovi
        .build();

    async function triggerCardAvailable(cardId) {
        if (service.proxy)
            await service.proxy.invokeMethodAsync("OnCardAvailable", cardId);
    }

    async function triggerReadersChanged(readers) {
        if (service.proxy)
            await service.proxy.invokeMethodAsync("OnReadersChanged", readers);
    }

    async function triggerConnected() {
        if (service.proxy) await service.proxy.invokeMethodAsync("OnConnected");
    }

    async function triggerDisconnected() {
        if (service.proxy) await service.proxy.invokeMethodAsync("OnDisconnected");
    }

    connection.on("OnCardAvailable", triggerCardAvailable);
    connection.on("OnReadersChanged", triggerReadersChanged);

    connection.onreconnected((connectionId) => {
        triggerConnected();
    });
    connection.onreconnecting((error) => {
        triggerDisconnected();
    });
    connection.onclose(async (error) => {
        triggerDisconnected();
        if (shouldBeConnected) await obj.start();
    });

    obj.getConnectionState = () => connection.state;

    obj.start = async () => {
        try {
            shouldBeConnected = true;
            await connection.start();
            triggerConnected();
        } catch (err) {
            triggerDisconnected();

            setTimeout(() => {
                if (shouldBeConnected && ["Disconnected"].includes(connection.state))
                    obj.start();
            }, 5000);
        }
    };
    obj.stop = async () => {
        shouldBeConnected = false;
        await connection.stop();
    };

    return obj;
})(cardReaderService);

export async function connectCardReaderService(dotNetProxy) {
    cardReaderService.proxy = dotNetProxy;
    await cardReaderConnection.start();
    return cardReaderConnection.getConnectionState();
}

export async function disconnectCardReaderService(dotNetProxy) {
    await cardReaderConnection.stop();
    cardReaderService.proxy = null;
}
