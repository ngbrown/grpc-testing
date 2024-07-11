import * as grpc from "@grpc/grpc-js";
import { GreeterClient } from "./gen/greet.grpc-client.js";

function main() {
  const client = new GreeterClient(
    "localhost:5274",
    grpc.credentials.createInsecure()
  );
  client.sayHello({ name: "node" }, (err, response) => {
    console.log(response?.message ?? err);
  });
}

main();
