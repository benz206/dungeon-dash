const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const market = require("../Assets/CloudCode/ArtifactMarket.js")._test;

function artifact(id = "0123456789abcdef0123456789abcdef") {
  const quality = 0.4;
  return {
    id,
    weaponId: "weapon_axe",
    displayName: "Axe",
    damage: 5 + Math.round(quality * 25),
    attacksPerSecond: 1.2 + quality * 2.3,
    criticalChance: 0.03 + quality * 0.27,
    quality: 1 + Math.round(quality * 99),
    rarity: "Rare",
  };
}

function params(action, extra = {}) {
  return { action, requestId: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", ...extra };
}

test("rejects impossible and unknown artifacts", () => {
  const impossible = artifact();
  impossible.damage = 30;
  assert.throws(() => market.validateArtifact(impossible), /possible game drop/);
  const unknown = artifact();
  unknown.weaponId = "weapon_cheat";
  assert.throws(() => market.validateArtifact(unknown), /catalog/);
});

test("server validation accepts every weapon in the Unity catalog", () => {
  const catalog = fs.readFileSync("Assets/Resources/GameCatalog.asset", "utf8");
  const weaponSection = catalog.split("  weapons:")[1].split("  floors:")[0];
  const catalogIds = [...weaponSection.matchAll(/- id: (weapon_[^\n]+)/g)].map(match => match[1]);
  assert.equal(catalogIds.length, 27);
  assert.deepEqual([...market.WEAPONS].sort(), catalogIds.sort());
});

test("list, buy, and claim transfer one artifact and the exact price", () => {
  const state = market.emptyState();
  market.applyAction(state, "seller", params("connect", { initialBalance: 0 }));
  market.applyAction(state, "buyer", params("connect", { initialBalance: 100 }));
  market.applyAction(state, "seller", params("list", { artifact: artifact(), price: 37 }));

  const bought = market.applyAction(state, "buyer", params("buy", { listingId: state.listings[0].id }));
  assert.equal(bought.artifact.id, artifact().id);
  assert.equal(state.players.buyer.balance, 63);
  assert.equal(state.players.seller.pendingCoins, 37);
  assert.equal(state.listings.length, 0);

  market.applyAction(state, "seller", params("claim"));
  assert.equal(state.players.seller.balance, 37);
  assert.equal(state.players.seller.pendingCoins, 0);
});

test("rejects insufficient balance, own purchases, and duplicate artifacts", () => {
  const state = market.emptyState();
  market.applyAction(state, "seller", params("connect", { initialBalance: 0 }));
  market.applyAction(state, "buyer", params("connect", { initialBalance: 2 }));
  market.applyAction(state, "seller", params("list", { artifact: artifact(), price: 5 }));
  const listingId = state.listings[0].id;
  assert.throws(() => market.applyAction(state, "buyer", params("buy", { listingId })), /Not enough/);
  assert.throws(() => market.applyAction(state, "seller", params("buy", { listingId })), /own listing/);
  assert.throws(() => market.applyAction(state, "seller", params("list", { artifact: artifact(), price: 5 })), /already listed/);
});

test("a second buyer loses the purchase race", () => {
  const state = market.emptyState();
  for (const id of ["seller", "buyer1", "buyer2"])
    market.applyAction(state, id, params("connect", { initialBalance: 100 }));
  market.applyAction(state, "seller", params("list", { artifact: artifact(), price: 25 }));
  const listingId = state.listings[0].id;
  market.applyAction(state, "buyer1", params("buy", { listingId }));
  assert.throws(() => market.applyAction(state, "buyer2", params("buy", { listingId })), /no longer available/);
  assert.equal(state.players.buyer2.balance, 100);
});

test("offline coin changes and listing cancellation preserve server ownership", () => {
  const state = market.emptyState();
  market.applyAction(state, "seller", params("connect", { initialBalance: 50 }));
  market.applyAction(state, "seller", params("syncCoins", { amount: -17 }));
  assert.equal(state.players.seller.balance, 33);
  assert.throws(
    () => market.applyAction(state, "seller", params("syncCoins", { amount: -34 })),
    /invalid/,
  );

  const item = artifact();
  market.applyAction(state, "seller", params("list", { artifact: item, price: 10 }));
  const listingId = state.listings[0].id;
  assert.throws(() => market.applyAction(state, "other", params("cancel", { listingId })), /own active/);
  const cancelled = market.applyAction(state, "seller", params("cancel", { listingId }));
  assert.equal(cancelled.artifact.id, item.id);
  assert.equal(state.owners[item.id], "seller");
});

test("write-lock conflict reloads and an idempotent retry charges once", async () => {
  let stored = market.emptyState();
  market.applyAction(stored, "buyer", params("connect", { initialBalance: 100 }));
  market.applyAction(stored, "seller", params("connect", { initialBalance: 0 }));
  market.applyAction(stored, "seller", params("list", { artifact: artifact(), price: 25 }));
  let lock = "one";
  let conflicts = 1;
  const api = {
    async getPrivateCustomItems() {
      return { data: { results: [{ value: structuredClone(stored), writeLock: lock }] } };
    },
    async setPrivateCustomItem(projectId, marketId, body) {
      if (conflicts-- > 0) throw { response: { status: 409 }, message: "conflict" };
      stored = structuredClone(body.value);
      lock = "two";
    },
  };
  const args = {
    params: params("buy", { listingId: stored.listings[0].id }),
    context: { projectId: "project", playerId: "buyer" },
    logger: { error() {} },
  };

  await market.runMarket(args, api);
  await market.runMarket(args, api);
  assert.equal(stored.players.buyer.balance, 75);
  assert.equal(stored.players.seller.pendingCoins, 25);
  assert.equal(stored.listings.length, 0);
});
