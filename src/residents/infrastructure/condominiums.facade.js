import { useCondominiumStore } from "../../condominiums/application/condominium.store.js";

/**
 * Anti-corruption layer between Residents and Condominiums bounded contexts.
 */
export class CondominiumsFacade {
    static async getCondominiums() {
        const store = useCondominiumStore();
        if (!store.condominiums.length) await store.fetchAll();
        return store.condominiums;
    }

    static getCondominiumName(id) {
        return useCondominiumStore().condominiums.find(c => c.id === Number(id))?.name || '';
    }
}
