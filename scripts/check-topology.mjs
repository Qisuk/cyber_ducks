// Validates infra/topology.yaml: every service has a known zone, every flow endpoint is a known
// service, and every boundary references known zones. Run with: npm run topology:check
import { readFileSync } from "node:fs";
import { parse } from "yaml";

const doc = parse(readFileSync("infra/topology.yaml", "utf8"));
const zones = Object.keys(doc.zones);
const services = Object.keys(doc.services);
const errors = [];

for (const [name, s] of Object.entries(doc.services)) {
  if (!zones.includes(s.zone)) errors.push(`service ${name}: unknown zone ${s.zone}`);
}
for (const flow of doc.flows) {
  for (const end of [].concat(flow.from, flow.to)) {
    if (!services.includes(end)) errors.push(`${flow.id}: unknown service ${end}`);
  }
  if (flow.boundary && !doc.boundaries[flow.boundary]) errors.push(`${flow.id}: unknown boundary ${flow.boundary}`);
  if (!flow.what || !flow.protocol) errors.push(`${flow.id}: missing what/protocol`);
}
for (const [id, b] of Object.entries(doc.boundaries)) {
  for (const z of b.between) if (z !== "all" && !zones.includes(z)) errors.push(`${id}: unknown zone ${z}`);
}

console.log(`zones ${zones.length}, services ${services.length}, flows ${doc.flows.length}, boundaries ${Object.keys(doc.boundaries).length}`);
if (errors.length) {
  console.error(errors.join("\n"));
  process.exit(1);
}
console.log("topology.yaml: all references valid");
