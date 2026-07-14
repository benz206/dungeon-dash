const MARKET_ID = "DUNGEON_DASH_MARKET";
const STATE_KEY = "state";
const MAX_LISTINGS = 200;
const MAX_REQUESTS = 400;
const MAX_RETRIES = 5;

const WEAPONS = new Set([
  "weapon_anime_sword", "weapon_arrow", "weapon_axe", "weapon_baton_with_spikes",
  "weapon_big_hammer", "weapon_bow", "weapon_bow_2", "weapon_cleaver",
  "weapon_double_axe", "weapon_duel_sword", "weapon_golden_sword",
  "weapon_green_magic_staff", "weapon_hammer", "weapon_katana", "weapon_knife",
  "weapon_knight_sword", "weapon_lavish_sword", "weapon_mace", "weapon_machete",
  "weapon_red_gem_sword", "weapon_red_magic_staff", "weapon_regular_sword",
  "weapon_rusty_sword", "weapon_saw_sword", "weapon_spear", "weapon_throwing_axe",
  "weapon_waraxe",
]);

module.exports = async (args) => runMarket(args);

module.exports.params = {
  action: { type: "String", required: true },
  requestId: "String",
  listingId: "String",
  price: "Numeric",
  amount: "Numeric",
  initialBalance: "Numeric",
  artifact: "JSON",
};

function emptyState() {
  return { version: 1, listings: [], players: {}, owners: {}, requests: [] };
}

function normalizeState(value) {
  const state = value && typeof value === "object" ? value : emptyState();
  state.version = 1;
  state.listings = Array.isArray(state.listings) ? state.listings : [];
  state.players = state.players && typeof state.players === "object" ? state.players : {};
  state.owners = state.owners && typeof state.owners === "object" ? state.owners : {};
  state.requests = Array.isArray(state.requests) ? state.requests : [];
  return state;
}

function player(state, playerId) {
  if (!state.players[playerId]) state.players[playerId] = { balance: 0, pendingCoins: 0 };
  return state.players[playerId];
}

function requireInteger(value, name, min, max) {
  if (!Number.isInteger(value) || value < min || value > max)
    throw Error(`${name} must be an integer from ${min} to ${max}.`);
  return value;
}

function validateArtifact(artifact) {
  if (!artifact || typeof artifact !== "object") throw Error("Artifact is required.");
  if (typeof artifact.id !== "string" || !/^[a-f0-9]{32}$/i.test(artifact.id))
    throw Error("Artifact ID is invalid.");
  if (!WEAPONS.has(artifact.weaponId)) throw Error("Weapon is not in the game catalog.");

  requireInteger(artifact.damage, "Damage", 5, 30);
  requireInteger(artifact.quality, "Quality", 1, 100);
  if (!Number.isFinite(artifact.attacksPerSecond) || !Number.isFinite(artifact.criticalChance))
    throw Error("Artifact stats must be finite numbers.");

  const speedQuality = (artifact.attacksPerSecond - 1.2) / 2.3;
  const critQuality = (artifact.criticalChance - 0.03) / 0.27;
  const scoreQuality = (artifact.quality - 1) / 99;
  if (speedQuality < -0.0001 || speedQuality > 1.0001 ||
      Math.abs(speedQuality - critQuality) > 0.002 ||
      Math.abs(speedQuality - scoreQuality) > 0.006 ||
      Math.abs(artifact.damage - (5 + Math.round(speedQuality * 25))) > 0.01)
    throw Error("Artifact stats do not match a possible game drop.");

  const expectedRarity = artifact.quality >= 85 ? "Mythic" :
    artifact.quality >= 60 ? "Epic" : artifact.quality >= 35 ? "Rare" : "Common";
  if (artifact.rarity !== expectedRarity) throw Error("Artifact rarity is invalid.");
  if (typeof artifact.displayName !== "string" || artifact.displayName.length < 1 || artifact.displayName.length > 80)
    throw Error("Artifact name is invalid.");

  return {
    id: artifact.id,
    weaponId: artifact.weaponId,
    displayName: artifact.displayName,
    damage: artifact.damage,
    attacksPerSecond: artifact.attacksPerSecond,
    criticalChance: artifact.criticalChance,
    quality: artifact.quality,
    rarity: artifact.rarity,
  };
}

function publicView(state, playerId, result) {
  const account = state.players[playerId] || { balance: 0, pendingCoins: 0 };
  return {
    ok: true,
    listings: state.listings,
    balance: account.balance,
    pendingCoins: account.pendingCoins,
    artifact: result && result.artifact ? result.artifact : null,
    message: result && result.message ? result.message : "",
  };
}

function applyAction(state, playerId, params) {
  const action = params.action;
  const account = player(state, playerId);

  if (action === "connect") {
    if (!account.initialized) {
      account.balance = requireInteger(params.initialBalance || 0, "Initial balance", 0, 100000);
      account.initialized = true;
    }
    return { message: "Connected" };
  }

  if (action === "syncCoins") {
    const amount = requireInteger(params.amount, "Coin change", -100000, 100000);
    if (amount === 0 || account.balance + amount < 0) throw Error("Coin change is invalid.");
    account.balance += amount;
    return { message: `Synchronized ${amount} gameplay coins` };
  }

  if (action === "list") {
    if (state.listings.length >= MAX_LISTINGS) throw Error("The market is full. Try again later.");
    const artifact = validateArtifact(params.artifact);
    const price = requireInteger(params.price, "Price", 1, 100000);
    const ownerId = state.owners[artifact.id];
    if (ownerId && ownerId !== playerId) throw Error("This artifact belongs to another player.");
    if (state.listings.some(x => x.artifact.id === artifact.id)) throw Error("This artifact is already listed.");

    state.owners[artifact.id] = playerId;
    state.listings.push({
      id: `${playerId}:${params.requestId}`,
      sellerId: playerId,
      price,
      artifact,
    });
    return { message: `Listed ${artifact.displayName}` };
  }

  const listingIndex = state.listings.findIndex(x => x.id === params.listingId);
  const listing = listingIndex >= 0 ? state.listings[listingIndex] : null;

  if (action === "buy") {
    if (!listing) throw Error("That listing is no longer available.");
    if (listing.sellerId === playerId) throw Error("You cannot buy your own listing.");
    if (account.balance < listing.price) throw Error("Not enough market coins.");

    account.balance -= listing.price;
    player(state, listing.sellerId).pendingCoins += listing.price;
    state.owners[listing.artifact.id] = playerId;
    state.listings.splice(listingIndex, 1);
    return { artifact: listing.artifact, message: `Bought ${listing.artifact.displayName}` };
  }

  if (action === "cancel") {
    if (!listing || listing.sellerId !== playerId) throw Error("You can only cancel your own active listing.");
    state.listings.splice(listingIndex, 1);
    return { artifact: listing.artifact, message: `Returned ${listing.artifact.displayName}` };
  }

  if (action === "claim") {
    const claimed = account.pendingCoins;
    account.pendingCoins = 0;
    account.balance += claimed;
    return { message: claimed > 0 ? `Claimed ${claimed} coins` : "No proceeds to claim" };
  }

  throw Error(`Unknown market action: ${action}`);
}

function isMutation(action) {
  return action !== "refresh";
}

async function loadState(api, projectId) {
  const response = await api.getPrivateCustomItems(projectId, MARKET_ID, [STATE_KEY]);
  const item = response.data.results[0];
  return { state: normalizeState(item && item.value), writeLock: item && item.writeLock };
}

function isConflict(error) {
  return error && error.response && error.response.status === 409;
}

async function runMarket({ params, context, logger }, apiOverride) {
  const api = apiOverride || new (require("@unity-services/cloud-save-1.4").DataApi)(context);
  const { projectId, playerId } = context;
  if (!projectId || !playerId) throw Error("An authenticated player is required.");

  if (!isMutation(params.action)) {
    const loaded = await loadState(api, projectId);
    return publicView(loaded.state, playerId);
  }

  if (typeof params.requestId !== "string" || !/^[a-f0-9]{32}$/i.test(params.requestId))
    throw Error("A valid request ID is required.");

  for (let attempt = 0; attempt < MAX_RETRIES; attempt++) {
    const loaded = await loadState(api, projectId);
    const prior = loaded.state.requests.find(x => x.playerId === playerId && x.requestId === params.requestId);
    if (prior) return publicView(loaded.state, playerId, prior.result);

    const result = applyAction(loaded.state, playerId, params);
    loaded.state.requests.push({ playerId, requestId: params.requestId, result });
    if (loaded.state.requests.length > MAX_REQUESTS)
      loaded.state.requests.splice(0, loaded.state.requests.length - MAX_REQUESTS);

    const body = { key: STATE_KEY, value: loaded.state };
    if (loaded.writeLock) body.writeLock = loaded.writeLock;
    try {
      await api.setPrivateCustomItem(projectId, MARKET_ID, body);
      return publicView(loaded.state, playerId, result);
    } catch (error) {
      if (!isConflict(error) || attempt === MAX_RETRIES - 1) {
        logger.error("Artifact market update failed", { message: error.message, action: params.action });
        throw error;
      }
    }
  }
}

module.exports._test = { WEAPONS, emptyState, normalizeState, validateArtifact, applyAction, runMarket };
