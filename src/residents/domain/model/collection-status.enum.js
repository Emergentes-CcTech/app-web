export const CollectionStatus = Object.freeze({ CURRENT: 'current', OVERDUE: 'overdue', PENDING: 'pending' });

export const collectionSeverity = (status) =>
    ({ current: 'success', overdue: 'danger', pending: 'secondary' }[status] || 'secondary');
