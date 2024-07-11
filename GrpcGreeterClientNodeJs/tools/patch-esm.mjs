#! /usr/bin/env node
import fs from "node:fs";

// This is needed because https://github.com/timostamm/protobuf-ts/pull/233 hasn't been merged and released

const collectFiles = (directory, fileList = []) => {
  fs.readdirSync(directory)
    .map((innerFile) => `${directory}/${innerFile}`)
    .forEach((file) =>
      fs.statSync(file).isDirectory()
        ? collectFiles(file, fileList)
        : fileList.push(file)
    );
  return fileList;
};

const files = collectFiles("./src/gen").filter((file) => file.endsWith(".ts"));

files.forEach((file) => {
  const content = fs.readFileSync(file, "utf8");
  const updated = content
    .split("\n")
    .map((s) =>
      s.replace(/(^import [^}]*} from ["']\..+(?<!\.js))(["'];)$/, "$1.js$2")
    )
    .join("\n");
  fs.writeFileSync(`${file}`, updated, "utf-8");
});
