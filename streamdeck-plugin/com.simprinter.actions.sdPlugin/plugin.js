// SimPrinter Stream Deck plugin.
//
// Connects to the Stream Deck app over the local WebSocket it launches this process with,
// and on a keyDown for one of our three actions, calls SimPrinter's local HTTP control
// server (see RemoteControlServer.cs in the SimPrinter project) to trigger the print.
// Requires SimPrinter to already be running - if it isn't, the HTTP call fails and the
// key shows a red "X" (showAlert).

const WebSocket = require('ws');
const http = require('http');

const SIMPRINTER_PORT = 47653;

const ACTION_ENDPOINTS = {
  'com.simprinter.actions.printflightplan': '/print/flightplan',
  'com.simprinter.actions.printpreliminary': '/print/preliminary',
  'com.simprinter.actions.printfinal': '/print/final',
};

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    if (argv[i].startsWith('-')) {
      args[argv[i].slice(1)] = argv[i + 1];
      i++;
    }
  }
  return args;
}

const args = parseArgs(process.argv.slice(2));
const streamDeckPort = args.port;
const pluginUUID = args.pluginUUID;
const registerEvent = args.registerEvent;

const streamDeck = new WebSocket(`ws://127.0.0.1:${streamDeckPort}`);

streamDeck.on('open', () => {
  streamDeck.send(JSON.stringify({ event: registerEvent, uuid: pluginUUID }));
});

streamDeck.on('message', (data) => {
  let message;
  try {
    message = JSON.parse(data.toString());
  } catch (err) {
    return;
  }

  if (message.event === 'keyDown') {
    handleKeyDown(message);
  }
});

streamDeck.on('error', () => {
  // Stream Deck manages the plugin process lifecycle (restarts it if needed) - nothing
  // for us to do here beyond not crashing the process.
});

function handleKeyDown(message) {
  const endpoint = ACTION_ENDPOINTS[message.action];
  if (!endpoint) return;

  const context = message.context;

  const request = http.request(
    {
      host: 'localhost',
      port: SIMPRINTER_PORT,
      path: endpoint,
      method: 'POST',
      timeout: 4000,
      headers: { 'Content-Length': 0 },
    },
    (response) => {
      response.resume(); // drain the body, we don't need it
      if (response.statusCode === 200) {
        sendToStreamDeck('showOk', context);
      } else {
        sendToStreamDeck('showAlert', context);
      }
    }
  );

  // SimPrinter isn't running, or something else went wrong reaching it.
  request.on('error', () => sendToStreamDeck('showAlert', context));
  request.on('timeout', () => request.destroy());
  request.end();
}

function sendToStreamDeck(event, context) {
  if (streamDeck.readyState === WebSocket.OPEN) {
    streamDeck.send(JSON.stringify({ event, context }));
  }
}
