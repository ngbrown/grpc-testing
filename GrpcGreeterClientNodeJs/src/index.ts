import { ChannelCredentials } from "@grpc/grpc-js";
import { GreeterClient } from "./gen/greet.client.js";
import { GrpcTransport } from "@protobuf-ts/grpc-transport";

const transport = new GrpcTransport({
  host: "localhost:5274",

  // Use h2c unencrypted instead of https
  channelCredentials: ChannelCredentials.createInsecure(),

  // Interceptors apply to all calls running through this transport.
  interceptors: [],
});

async function main() {
  const client = new GreeterClient(transport);
  const res = await client.sayHello({ name: "node" });
  console.log(res.response.message);
}

void main();
