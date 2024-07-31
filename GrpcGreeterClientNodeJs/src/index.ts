import { Code, ConnectError, createPromiseClient } from "@connectrpc/connect";
import { createGrpcTransport } from "@connectrpc/connect-node";
import { Greeter } from "./gen/greet_connect.js";

const transport = createGrpcTransport({
  // Requests will be made to <baseUrl>/<package>.<service>/method
  baseUrl: "http://localhost:5274",

  // You have to tell the Node.js http API which HTTP version to use.
  httpVersion: "2",

  // Interceptors apply to all calls running through this transport.
  interceptors: [],
});

async function main() {
  const client = createPromiseClient(Greeter, transport);
  const res = await client.sayHello({ name: "node" });
  console.log(res.message);

  // expected to fail
  try {
    await client.sayHello({ name: "nobody" });
  } catch (err) {
    if (err instanceof ConnectError) {
      console.error(
        `${err.name}: StatusCode=${Code[err.code]}, Detail=${err.message}`
      );
      if (err.code !== Code.PermissionDenied) {
        throw err;
      }
    } else {
      throw err;
    }
  }
}

void main();
